using UnityEngine;

namespace MainGame.Network.Event
{
    public class ChunkUpdateEvent
    {
        public delegate void OnChunkUpdate(OnChunkUpdateEventProperties properties);
        public delegate void OnBlockUpdate(OnBlockUpdateEventProperties properties);
        private event OnChunkUpdate OnChunkUpdateEvent;
        private event OnBlockUpdate OnBlockUpdateEvent;
        public void Subscribe(OnChunkUpdate onChunkUpdate, OnBlockUpdate onBlockUpdate)
        {
            OnChunkUpdateEvent += onChunkUpdate;
            OnBlockUpdateEvent += onBlockUpdate;
        }

        public void Unsubscribe(OnChunkUpdate onChunkUpdate, OnBlockUpdate onBlockUpdate)
        {
            OnChunkUpdateEvent -= onChunkUpdate;
            OnBlockUpdateEvent -= onBlockUpdate;
        }

        public void InvokeChunkUpdateEvent(OnChunkUpdateEventProperties properties)
        {
            OnChunkUpdateEvent?.Invoke(properties);
        }
        public void InvokeBlockUpdateEvent(OnBlockUpdateEventProperties properties)
        {
            OnBlockUpdateEvent?.Invoke(properties);
        }

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