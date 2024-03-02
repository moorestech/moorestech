using MessagePack;

namespace Server.Protocol
{
    [MessagePackObject]
    public class ProtocolMessagePackBase
    {
        [Key(0)]
        public string Tag { get; set; }
        
        [Key(1)]
        public int SequenceId { get; set; }
    }
}