using System;
using System.Threading;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class NetworkReceivedChunkDataEvent
    {
        private SynchronizationContext _mainThread;
        
        public NetworkReceivedChunkDataEvent()
        {
            //Unityではメインスレッドでしか実行できないのでメインスレッドを保存しておく
            _mainThread = SynchronizationContext.Current;
        }
        
        public event Action<ChunkUpdateEventProperties> OnChunkUpdateEvent;
        public event Action<BlockUpdateEventProperties> OnBlockUpdateEvent;

        public void InvokeChunkUpdateEvent(ChunkUpdateEventProperties properties)
        {
            _mainThread.Post(_ => OnChunkUpdateEvent?.Invoke(properties), null);
        }
        public void InvokeBlockUpdateEvent(BlockUpdateEventProperties properties)
        {
            _mainThread.Post(_ => OnBlockUpdateEvent?.Invoke(properties), null);
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