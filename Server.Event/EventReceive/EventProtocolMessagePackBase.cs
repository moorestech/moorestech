using MessagePack;

namespace Server.Event.EventReceive
{
    [MessagePackObject(keyAsPropertyName:true)]
    public class EventProtocolMessagePackBase
    {
        public string EventTag { get; set; }
    }
}