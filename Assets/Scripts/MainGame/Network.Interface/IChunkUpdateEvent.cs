using UnityEngine;

namespace MainGame.Network.Interface
{
    public interface IChunkUpdateEvent
    {
        public delegate void OnChunkUpdate(OnChunkUpdateEventProperties properties);
        public delegate void OnBlockUpdate(OnBlockUpdateEventProperties properties);
        
        public void Subscribe(OnChunkUpdate onChunkUpdate,OnBlockUpdate onBlockUpdate);
        public void Unsubscribe(OnChunkUpdate onChunkUpdate,OnBlockUpdate onBlockUpdate);
    }

    public class OnChunkUpdateEventProperties
    {
        public readonly Vector2Int ChunkPos;
        public readonly int[,] BlockIds;

        public OnChunkUpdateEventProperties(Vector2Int chunkPos, int[,] blockIds)
        {
            this.ChunkPos = chunkPos;
            this.BlockIds = blockIds;
        }
    }

    public class OnBlockUpdateEventProperties
    {
        public readonly Vector2Int BlockPos;
        public readonly  int BlockId;

        public OnBlockUpdateEventProperties(Vector2Int blockPos, int blockId)
        {
            this.BlockPos = blockPos;
            this.BlockId = blockId;
        }
    }
}