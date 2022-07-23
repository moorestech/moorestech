using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveChunkDataEvent
    {
        public event Action<ChunkUpdateEventProperties> OnChunkUpdateEvent;
        public event Action<BlockUpdateEventProperties> OnBlockUpdateEvent;

        internal async UniTask InvokeChunkUpdateEvent(ChunkUpdateEventProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnChunkUpdateEvent?.Invoke(properties);
        }
        
        internal async UniTask InvokeBlockUpdateEvent(BlockUpdateEventProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnBlockUpdateEvent?.Invoke(properties);
        }
    }
    
    

    public class ChunkUpdateEventProperties
    {
        public readonly Vector2Int ChunkPos;
        public readonly int[,] BlockIds;
        public readonly BlockDirection[,] BlockDirections;
        
        public readonly int[,] MapTileIds;

        public ChunkUpdateEventProperties(Vector2Int chunkPos, int[,] blockIds, BlockDirection[,] blockDirections, int[,] mapTileIds)
        {
            ChunkPos = chunkPos;
            BlockIds = blockIds;
            BlockDirections = blockDirections;
            MapTileIds = mapTileIds;
        }
    }

    public class BlockUpdateEventProperties
    {
        public readonly Vector2Int BlockPos;
        public readonly  int BlockId;
        public readonly BlockDirection BlockDirection;

        public BlockUpdateEventProperties(Vector2Int blockPos, int blockId, BlockDirection blockDirection)
        {
            this.BlockPos = blockPos;
            this.BlockId = blockId;
            BlockDirection = blockDirection;
        }
    }
}