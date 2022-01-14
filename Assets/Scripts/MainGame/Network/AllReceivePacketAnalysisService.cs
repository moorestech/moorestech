using System.Collections.Generic;
using MainGame.Network.Receive;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly List<IAnalysisPacket> _analysisPacketList = new List<IAnalysisPacket>();

        public AllReceivePacketAnalysisService()
        {
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new DummyProtocol());
        }

        public void Analysis(byte[] bytes)
        {
            
        }
    }
}