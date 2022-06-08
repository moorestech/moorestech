using System.Collections.Generic;

namespace MainGame.Network.Receive
{
    public class ReciveDummyProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> data) { }
    }
}