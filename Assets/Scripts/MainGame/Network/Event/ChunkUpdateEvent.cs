using MainGame.Network.Interface;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ChunkUpdateEvent : IChunkUpdateEvent
    {
        private event IChunkUpdateEvent.OnChunkUpdate OnChunkUpdateEvent;
        private event IChunkUpdateEvent.OnBlockUpdate OnBlockUpdateEvent;
        public void Subscribe(IChunkUpdateEvent.OnChunkUpdate onChunkUpdate, IChunkUpdateEvent.OnBlockUpdate onBlockUpdate)
        {
            OnChunkUpdateEvent += onChunkUpdate;
            OnBlockUpdateEvent += onBlockUpdate;
        }

        public void Unsubscribe(IChunkUpdateEvent.OnChunkUpdate onChunkUpdate, IChunkUpdateEvent.OnBlockUpdate onBlockUpdate)
        {
            OnChunkUpdateEvent -= onChunkUpdate;
            OnBlockUpdateEvent -= onBlockUpdate;
        }

        public void OnOnChunkUpdateEvent(Vector2Int chunkPos, int[,] blockIds)
        {
            OnChunkUpdateEvent?.Invoke(chunkPos, blockIds);
        }
        public void OnOnBlockUpdateEvent(Vector2Int blockPos, int blockId)
        {
            OnBlockUpdateEvent?.Invoke(blockPos, blockId);
        }

    }
}