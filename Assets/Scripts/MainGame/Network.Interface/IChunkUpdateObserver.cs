using UnityEngine;

namespace MainGame.Network.Interface
{
    public interface IChunkUpdateObserver
    {
        public void UpdateChunk(Vector2Int chunkPosition,int[,] ids);
        public void UpdateBlock(Vector2Int blockPosition, int id);
    }
}