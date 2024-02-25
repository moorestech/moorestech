using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    public class EventProtocol : IPacketResponse
    {
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EventProtocol(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EventProtocolMessagePack>(payload.ToArray());

            //イベントプロトコルプロバイダからデータを取得して返す
            var events = _eventProtocolProvider.GetEventBytesList(data.PlayerId);
            
            return new ResponseEventProtocolMessagePack(events);
        }
    }

    [MessagePackObject(true)]
    public class EventProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EventProtocolMessagePack()
        {
        }

        public EventProtocolMessagePack(int playerId)
        {
            Tag = EventProtocolMessagePackBase.EventProtocolTag;
            PlayerId = playerId;
        }

        public int PlayerId { get; set; }
    }

    [MessagePackObject(true)]
    public class ResponseEventProtocolMessagePack : ProtocolMessagePackBase
    {
        public List<EventMessagePack> Events { get; set; }
        
        public ResponseEventProtocolMessagePack(List<EventMessagePack> events)
        {
            Events = events;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseEventProtocolMessagePack() { }
    }
}