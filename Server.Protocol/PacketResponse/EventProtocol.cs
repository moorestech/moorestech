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
            //パケットのパース、接続元、接続先のインスタンス取得
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var userId = byteListEnumerator.MoveNextToGetInt();
            return _eventProtocolProvider.GetEventBytesList(userId);
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EventProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}