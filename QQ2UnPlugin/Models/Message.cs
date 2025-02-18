using System.Xml.Serialization;

namespace beihuNF.QQ2UnPlugin.Models
{
    public sealed class Message
    {
        public Message(string text, string iconUrl, string color)
        {
            Text = text;
            IconUrl = iconUrl;
            Color = color;
        }

        public Message() { }

        [XmlAttribute]
        public string Text { get; set; }
        [XmlAttribute]
        public string IconUrl { get; set; }
        public bool ShouldSerializeIconUrl() => !string.IsNullOrEmpty(IconUrl);
        [XmlAttribute]
        public string Color { get; set; }
        public bool ShouldSerializeColor() => !string.IsNullOrEmpty(Color);
    }
}
