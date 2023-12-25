using System;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using Constant;
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
        public readonly BlockDirection[,] BlockDirections;
        public readonly int[,] BlockIds;
        public readonly Vector2Int ChunkPos;

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
        public readonly BlockDirection BlockDirection;
        public readonly int BlockId;
        public readonly Vector2Int BlockPos;

        public BlockUpdateEventProperties(Vector2Int blockPos, int blockId, BlockDirection blockDirection)
        {
            BlockPos = blockPos;
            BlockId = blockId;
            BlockDirection = blockDirection;
        }
    }
}