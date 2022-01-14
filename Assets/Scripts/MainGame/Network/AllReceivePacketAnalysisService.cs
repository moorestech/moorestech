using System.Collections.Generic;
using MainGame.GameLogic.Interface;
using MainGame.Network.Receive;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly List<IAnalysisPacket> _analysisPacketList = new List<IAnalysisPacket>();

        public AllReceivePacketAnalysisService(IChunkDataStore chunkDataStore)
        {
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new ReceiveChunkDataProtocol(chunkDataStore));
        }

        public void Analysis(byte[] bytes)
        {
            
        }
    }
}