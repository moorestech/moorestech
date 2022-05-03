using System.Collections.Generic;

namespace MainGame.Network.Receive
{
    public interface IAnalysisPacket
    {
        public void Analysis(List<byte> data);
    }
}