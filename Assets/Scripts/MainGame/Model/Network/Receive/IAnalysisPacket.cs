using System.Collections.Generic;

namespace MainGame.Model.Network.Receive
{
    public interface IAnalysisPacket
    {
        public void Analysis(List<byte> data);
    }
}