using System.Collections.Generic;
using Server.Event;
using Server.PacketHandle.PacketResponse;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class SendEventProtocol : IPacketResponse
    {
        private readonly EventProtocolProvider _eventProtocolProvider;

        public SendEventProtocol(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var userId = byteListEnumerator.MoveNextToGetInt();
            return _eventProtocolProvider.GetEventBytesList(userId);
        }
    }
}