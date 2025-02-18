using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;
using beihuNF.QQ2UnPlugin.Models;

namespace beihuNF.QQ2UnPlugin
{
    public class QQ2UnPluginConfiguration : IRocketPluginConfiguration
    {
        public int Port { get; set; }
        public string MessageColor { get; set; }
        public string MessageIconUrl { get; set; }
        [XmlArrayItem("Message")]
        public List<Message> Messages { get; set; }
        public bool EnableWelcomeMessage { get; set; }
        public Message WelcomeMessage { get; set; }

        public void LoadDefaults()
        {
            Port = 8080;
            MessageColor = "white";
            MessageIconUrl = "{server_icon}";
            Messages =
            [
                new("Format examples: {b}文字加粗{/b}, {color=#e74c3c}文字颜色{/color}, {size=20}文字大小{/size}", "{server_icon}", "white")
            ];
            EnableWelcomeMessage = true;
            WelcomeMessage = new Message("{size=18}欢迎来到服务器 {b}{player_name}{/b}!", "{server_icon}", "white");
        }
    }
}