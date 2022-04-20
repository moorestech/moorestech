using System.Collections.Generic;

namespace MainGame.Model.Network.Receive
{
    public class DummyProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> data) { }
    }
}