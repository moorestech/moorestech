using System.Collections.Generic;
using MainGame.Network.Receive.Event;
using MainGame.Network.Util;

namespace MainGame.Network.Receive
{
    public class ReceiveEventProtocol : IAnalysisPacket
    {
        List<IAnalysisEventPacket> _eventPacketList = new List<IAnalysisEventPacket>();

        /// <summary>
        /// イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
        }
    }
}