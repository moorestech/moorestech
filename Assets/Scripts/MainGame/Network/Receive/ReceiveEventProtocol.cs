using System.Collections.Generic;
using MainGame.Network.Interface;
using MainGame.Network.Receive.EventPacket;
using MainGame.Network.Util;

namespace MainGame.Network.Receive
{
    public class ReceiveEventProtocol : IAnalysisPacket
    {
        List<IAnalysisEventPacket> _eventPacketList = new List<IAnalysisEventPacket>();

        public ReceiveEventProtocol(IChunkUpdateEvent chunkUpdateEvent)
        {
            _eventPacketList.Add(new BlockPlaceEvent(chunkUpdateEvent));
        }
        
        /// <summary>
        /// イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            bytes.MoveNextToGetShort();
            var eventId = bytes.MoveNextToGetShort();
            _eventPacketList[eventId].Analysis(data);
        }
    }
}