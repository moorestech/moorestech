using System.Collections.Generic;

namespace MainGame.Network.Receive
{
    public class ReceiveDummyProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> data)
        {
        }
    }
}