using System.Collections.Generic;

namespace MainGame.Network.Receive.Event
{
    public interface IAnalysisEventPacket
    {
        public void Analysis(List<byte> packet);
    }
}