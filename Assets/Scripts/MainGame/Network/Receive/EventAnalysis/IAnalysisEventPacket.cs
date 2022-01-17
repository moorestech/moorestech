using System.Collections.Generic;

namespace MainGame.Network.Receive.EventAnalysis
{
    public interface IAnalysisEventPacket
    {
        public void Analysis(List<byte> packet);
    }
}