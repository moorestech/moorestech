using System.Collections.Generic;

namespace MainGame.Model.Network.Receive.EventPacket
{
    public interface IAnalysisEventPacket
    {
        public void Analysis(List<byte> packet);
    }
}