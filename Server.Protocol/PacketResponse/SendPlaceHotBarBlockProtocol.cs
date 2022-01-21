using System.Collections.Generic;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        public List<byte[]> GetResponse(List<byte> payload)
        {
            var packet = new ByteArrayEnumerator(payload);
            packet.MoveNextToGetShort();
            var slot = packet.MoveNextToGetShort();
            var x = packet.MoveNextToGetInt();
            var y = packet.MoveNextToGetInt();
        }
    }
}