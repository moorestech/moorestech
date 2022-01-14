using System.Collections.Generic;
using MainGame.Network.Receive;

namespace MainGame.Network
{
    public class ReceivePacketAnalysis
    {
        private readonly List<IAnalysisPacket> _analysisPacketList = new List<IAnalysisPacket>();

        public ReceivePacketAnalysis()
        {
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new DummyProtocol());
        }

        public void Analysis(byte[] bytes)
        {
            
        }
    }
}