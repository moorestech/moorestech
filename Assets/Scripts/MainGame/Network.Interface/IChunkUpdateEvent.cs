using UnityEngine;

namespace MainGame.Network.Interface
{
    public interface IChunkUpdateEvent
    {
        public delegate void OnChunkUpdate(Vector2Int chunkPos,int[,] blockIds);
        public delegate void OnBlockUpdate(Vector2Int blockPos,int blockId);
        
        public void Subscribe(OnChunkUpdate onChunkUpdate,OnBlockUpdate onBlockUpdate);
        public void Unsubscribe(OnChunkUpdate onChunkUpdate,OnBlockUpdate onBlockUpdate);
    }
}