using System.Collections.Generic;

namespace MainGame.Network.Receive.EventPacket
{
    public interface IAnalysisEventPacket
    {
        public void Analysis(List<byte> packet);
    }
}