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

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EventProtocolMessagePack>(payload.ToArray());

            
            return _eventProtocolProvider.GetEventBytesList(data.PlayerId);
        }
    }

    [MessagePackObject(true)]
    public class EventProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
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
}