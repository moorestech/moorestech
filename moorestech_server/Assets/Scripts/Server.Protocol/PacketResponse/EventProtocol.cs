using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event;

namespace Server.Protocol.PacketResponse
{
    public class EventProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:event";
        
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
        
        
        
        [MessagePackObject]
        public class EventProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public EventProtocolMessagePack() { }
            
            public EventProtocolMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }
        }
        
        [MessagePackObject]
        public class ResponseEventProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<EventMessagePack> Events { get; set; }
            
            public ResponseEventProtocolMessagePack(List<EventMessagePack> events)
            {
                Tag = ProtocolTag;
                Events = events;
            }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseEventProtocolMessagePack() { }
        }
    }
}