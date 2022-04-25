using System.Collections.Generic;
using MainGame.Model.Network.Receive;

namespace MainGame.Network.Receive
{
    public class DummyProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> data) { }
    }
}