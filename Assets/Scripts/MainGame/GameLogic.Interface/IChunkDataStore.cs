using UnityEngine;

namespace MainGame.GameLogic.Interface
{
    public interface IChunkDataStore
    {
        public void SetChunk(Vector2Int chunkPosition,int[,] ids);
        public void SetBlock(Vector2Int blockPosition, int id);
    }
}