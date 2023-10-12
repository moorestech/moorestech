using MessagePack;

namespace Server.Protocol
{
    [MessagePackObject(true)]
    public class ProtocolMessagePackBase
    {
        public string Tag { get; set; }
    }
}