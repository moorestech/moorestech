using System;
using MessagePack;
using Server.Protocol.Base;

namespace Server.Event.EventReceive
{
    [MessagePackObject(keyAsPropertyName:true)]
    public class EventProtocolMessagePackBase : ToClientProtocolMessagePackBase
    {
        public const string EventProtocolTag = "va:event";
        public new string ToClientTag = EventProtocolTag;
        
        public string EventTag { get; set; }
        
    }
}