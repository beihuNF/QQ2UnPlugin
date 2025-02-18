using beihuNF.QQ2UnPlugin.Models;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace beihuNF.QQ2UnPlugin
{
    public class QQ2UnPlugin : RocketPlugin<QQ2UnPluginConfiguration>
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenerTask;

        public static QQ2UnPlugin Instance { get; private set; }
        public Color MessageColor { get; set; }
        public Message QQUNMessage { get; set; }

        protected override void Load()
        {
            Instance = this;
            if (!string.IsNullOrEmpty(Configuration.Instance.MessageColor))
            {
                MessageColor = UnturnedChat.GetColorFromName(Configuration.Instance.MessageColor, Color.green);
            }
            else
            {
                MessageColor = Color.green;
            }            
            StartHttpServer();
            U.Events.OnPlayerConnected += OnPlayerConnected;
            Logger.Log($"{Name} {Assembly.GetName().Version} 已加载!", ConsoleColor.Yellow);
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            StopHttpServer();
            Logger.Log($"{Name} 已卸载!", ConsoleColor.Yellow);
        }

        public override TranslationList DefaultTranslations => new()
        {
            { "Commands", "[[b]]Your commands:[[/b]] {0}" }
        };

        private void StartHttpServer()
        {
            _listener = new HttpListener();
            string urlPrefix = $"http://*:{Configuration.Instance.Port}/";
            _listener.Prefixes.Add(urlPrefix);
            try
            {
                _listener.Start();
            }
            catch (HttpListenerException e)
            {
                Logger.LogError("启动 HTTP 服务器失败，请检查是否拥有足够权限及端口是否被占用: " + e.Message);
                return;
            }
            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => Listen(_cts.Token));
            Logger.Log($"HTTP 服务器已启动，正在监听 {urlPrefix}");
        }
        private void StopHttpServer()
        {
            try
            {
                _cts?.Cancel();
                if (_listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    SendMessage(context);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Logger.LogException(ex);
                }
            }
        }

        private void SendMessage(HttpListenerContext context)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                if (Provider.clients.Count <= 0)
                {
                    return;
                }

                if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                    context.Request.Url.AbsolutePath.Equals("/api/qq-un", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string requestBody;
                        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                        {
                            requestBody = reader.ReadToEnd();
                        }

                        var requestData = JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);
                        if (requestData == null || !requestData.ContainsKey("message"))
                        {
                            throw new Exception("无效的请求数据");
                        }

                        string messageText = requestData["message"];
                        string iconUrl = requestData.ContainsKey("url") ? requestData["url"] : Configuration.Instance.MessageIconUrl;

                        //Logger.Log("收到来自 QQ 群的消息: " + messageText);

                        QQUNMessage = new Message(messageText, iconUrl, "white");

                        foreach (Player player in PlayerTool.EnumeratePlayers())
                        {
                            SendMessageToPlayer(UnturnedPlayer.FromPlayer(player), messageText, null, iconUrl, Configuration.Instance.MessageColor);
                        }

                        byte[] responseBytes = Encoding.UTF8.GetBytes("消息已成功发送到Unturned");
                        context.Response.ContentLength64 = responseBytes.Length;
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("消息处理错误: " + ex.Message);
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        byte[] errorBytes = Encoding.UTF8.GetBytes("请求格式错误");
                        context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                        context.Response.OutputStream.Close();
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.OutputStream.Close();
                }
            });
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            if (Configuration.Instance.EnableWelcomeMessage)
            {
                Message msg = Configuration.Instance.WelcomeMessage;
                string iconUrl = msg.IconUrl ?? Configuration.Instance.MessageIconUrl;
                SendMessageToPlayer(player, msg.Text, null, iconUrl, msg.Color);
            }
        }

        internal void SendMessageToPlayer(IRocketPlayer player, string translationKey, object[] placeholder = null, string iconUrl = null, string color = null)
        {
            string msg;
            if (DefaultTranslations.Any(x => x.Id == translationKey))
            {
                if (placeholder == null)
                {
                    placeholder = [];
                }
                msg = Translate(translationKey, placeholder);
                ReplaceVariables(ref msg, player);
                msg = msg.Replace("[[", "<").Replace("]]", ">");
            }
            else
            {
                msg = translationKey;
                ReplaceVariables(ref msg, player);
                msg = msg.Replace("{", "<").Replace("}", ">");
            }            

            if (player is ConsolePlayer)
            {
                msg = msg.Replace("<b>", "").Replace("</b>", "");
                Logger.Log(msg);
                return;
            }

            SteamPlayer steamPlayer = null;
            if (player != null)
            {
                UnturnedPlayer unturnedPlayer = (UnturnedPlayer)player;
                steamPlayer = unturnedPlayer.SteamPlayer();
            }

            if (iconUrl == null)
            {
                iconUrl = Configuration.Instance.MessageIconUrl;
            }

            ReplaceVariables(ref iconUrl, player);

            Color messageColor;
            if (color != null)
            {
                messageColor = UnturnedChat.GetColorFromName(color, MessageColor);
            }
            else
            {
                messageColor = MessageColor;
            }

            ChatManager.serverSendMessage(msg, messageColor, null, steamPlayer, EChatMode.SAY, iconUrl, true);
        }

        internal void ReplaceVariables(ref string text, IRocketPlayer player)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            text = text
                .Replace("{player_name}", player?.DisplayName ?? string.Empty)
                .Replace("{player_id}", player?.Id ?? string.Empty)
                .Replace("{server_name}", Provider.serverName)
                .Replace("{server_players}", Provider.clients.Count.ToString("N0"))
                .Replace("{server_maxplayers}", Provider.maxPlayers.ToString("N0"))
                .Replace("{server_map}", Level.info?.name ?? string.Empty)
                .Replace("{server_mode}", Provider.mode.ToString())
                .Replace("{server_thumbnail}", Provider.configData.Browser.Thumbnail)
                .Replace("{server_icon}", Provider.configData.Browser.Icon);
        }
    }
}
