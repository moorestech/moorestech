using MainGame.Basic;
using UnityEngine;
using static MainGame.Network.Event.INetworkReceivedChunkDataEvent;

namespace MainGame.Network.Event
{
    public interface INetworkReceivedChunkDataEvent
    {
        public delegate void ChunkUpdate(OnChunkUpdateEventProperties properties);
        public delegate void BlockUpdate(OnBlockUpdateEventProperties properties);
        public void Subscribe(ChunkUpdate onChunkUpdate, BlockUpdate onBlockUpdate);
    }
    public class NetworkReceivedChunkDataEvent : INetworkReceivedChunkDataEvent
    {
        private event ChunkUpdate OnChunkUpdateEvent;
        private event BlockUpdate OnBlockUpdateEvent;
        public void Subscribe(ChunkUpdate onChunkUpdate, BlockUpdate onBlockUpdate)
        {
            OnChunkUpdateEvent += onChunkUpdate;
            OnBlockUpdateEvent += onBlockUpdate;
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
        public readonly BlockDirection[,] BlockDirections;

        public OnChunkUpdateEventProperties(Vector2Int chunkPos, int[,] blockIds, BlockDirection[,] blockDirections)
        {
            this.ChunkPos = chunkPos;
            this.BlockIds = blockIds;
            BlockDirections = blockDirections;
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