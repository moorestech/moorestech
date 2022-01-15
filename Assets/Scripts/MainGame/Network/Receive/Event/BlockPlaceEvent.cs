using System.Collections.Generic;
using MainGame.GameLogic.Interface;

namespace MainGame.Network.Receive.Event
{
    public class BlockPlaceEvent : IAnalysisEventPacket
    {
        IChunkDataStore _chunkDataStore;

        public BlockPlaceEvent(IChunkDataStore chunkDataStore)
        {
            _chunkDataStore = chunkDataStore;
        }

        public void Analysis(List<byte> packet)
        {
        }
    }
}