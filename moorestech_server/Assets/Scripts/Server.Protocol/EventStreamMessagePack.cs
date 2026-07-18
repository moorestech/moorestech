using System;
using MessagePack;
using Server.Event;

namespace Server.Protocol
{
    // push配信イベント1件のenvelope
    // Envelope for one pushed event
    [MessagePackObject]
    public class EventStreamMessagePack : ProtocolMessagePackBase
    {
        public const string ProtocolTag = "va:event";

        [Key(2)] public EventMessagePack Event { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EventStreamMessagePack() { }

        public EventStreamMessagePack(EventMessagePack eventMessagePack)
        {
            Tag = ProtocolTag;
            Event = eventMessagePack;
        }
    }
}
