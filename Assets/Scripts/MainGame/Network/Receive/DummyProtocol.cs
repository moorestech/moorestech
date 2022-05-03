using System.Collections.Generic;

namespace MainGame.Network.Receive
{
    public class DummyProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> data) { }
    }
}