using System.Collections.Generic;
using Server.Event;
using Server.Util;

namespace Server.PacketHandle.PacketResponse
{
    public class SendEventProtocol : IPacketResponse
    {
        private readonly EventProtocolQueProvider _eventProtocolQueProvider;
        public SendEventProtocol(EventProtocolQueProvider eventProtocolQueProvider)
        {
            _eventProtocolQueProvider = eventProtocolQueProvider;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var b = new ByteArrayEnumerator(payload);
            b.MoveNextToGetShort();
            var userId = b.MoveNextToGetInt();
            return _eventProtocolQueProvider.GetEventBytesList(userId);
        }
    }
}