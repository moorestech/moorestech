using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class EventProtocol : IPacketResponse
    {
        public const string Tag = "va:event";
        
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EventProtocol(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EventProtocolMessagePack>(payload.ToArray());
            
            //イベントプロトコルプロバイダからデータを取得して返す
            return _eventProtocolProvider.GetEventBytesList(data.PlayerId);
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EventProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
    }
}