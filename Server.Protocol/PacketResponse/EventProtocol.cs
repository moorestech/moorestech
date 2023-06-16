using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol.Base;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class EventProtocol : IPacketResponse
    {
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EventProtocol(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EventProtocolMessagePack>(payload.ToArray());
            
            //イベントプロトコルプロバイダからデータを取得して返す
            return _eventProtocolProvider.GetEventBytesList(data.PlayerId);
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EventProtocolMessagePack : ToServerProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EventProtocolMessagePack() { }
        public EventProtocolMessagePack(int playerId)
        {
            ToServerTag = EventProtocolMessagePackBase.EventProtocolTag;
            PlayerId = playerId;
        }

        public int PlayerId { get; set; }
    }
}