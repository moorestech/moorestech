using UnityEngine;

namespace MainGame.GameLogic.Interface
{
    public interface IChunkDataStore
    {
        public void SetChunk(Vector2Int chunkPosition,int[,] ids);
    }
}