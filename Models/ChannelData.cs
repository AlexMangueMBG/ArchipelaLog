using System.Xml.Serialization;

namespace ArchipelaLog.Models
{
    public class ChannelData
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string Hostname { get; set; }
        public int Port { get; set; }
        public List<string> RolesThatCanAttach { get; set; }
        public List<UserData> Users{ get; set; }

        [XmlIgnore]
        public Archipelago ArchipelagoSession;
    }
}
