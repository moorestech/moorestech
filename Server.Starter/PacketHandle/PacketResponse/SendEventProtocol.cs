using System.Collections.Generic;
using Server.Event;
using Server.Util;

namespace Server.PacketHandle.PacketResponse
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
            var b = new ByteArrayEnumerator(payload);
            b.MoveNextToGetShort();
            var userId = b.MoveNextToGetInt();
            return _eventProtocolProvider.GetEventBytesList(userId);
        }
    }
}