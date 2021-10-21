using System.Collections.Generic;
using Server.Util;

namespace Server.PacketHandle.PacketResponse
{
    public static class SendEventProtocol
    {
        public static List<byte[]> GetResponse(byte[] payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var b = new ByteArrayEnumerator(payload);
            b.MoveNextToGetShort();
            var userId = b.MoveNextToGetInt();
            return null;
        }
    }
}