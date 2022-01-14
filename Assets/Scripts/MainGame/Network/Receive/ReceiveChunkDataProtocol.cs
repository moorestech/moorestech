using MainGame.GameLogic.Interface;

namespace MainGame.Network.Receive
{
    public class ReceiveChunkDataProtocol : IAnalysisPacket
    {
        private IChunkDataStore _chunkDataStore;
        public void Analysis(byte[] data)
        {
            throw new System.NotImplementedException();
        }
    }
}