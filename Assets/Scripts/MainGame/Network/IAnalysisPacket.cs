using System.Collections.Generic;

namespace MainGame.Network
{
    public interface IAnalysisPacket
    {
        public void Analysis(List<byte> data);
    }
}