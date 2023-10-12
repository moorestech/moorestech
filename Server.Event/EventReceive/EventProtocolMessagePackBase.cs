using MessagePack;

namespace Server.Event.EventReceive
{
    [MessagePackObject(true)]
    public class EventProtocolMessagePackBase
    {
        public const string EventProtocolTag = "va:event";

        public string Tag = EventProtocolTag;
        public string EventTag { get; set; }
    }
}