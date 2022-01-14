using UnityEngine;

namespace MainGame.GameLogic.Interface
{
    public interface IChunkDataStore
    {
        public void SetChunk(Vector2 chunkPosition,int[,] ids);
    }
}