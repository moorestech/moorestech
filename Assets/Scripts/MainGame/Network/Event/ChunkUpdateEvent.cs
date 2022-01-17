using MainGame.Network.Interface;
using UnityEngine;
using static MainGame.Network.Interface.IChunkUpdateEvent;

namespace MainGame.Network.Event
{
    public class ChunkUpdateEvent : IChunkUpdateEvent
    {
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

        internal void InvokeChunkUpdateEvent(OnChunkUpdateEventProperties properties)
        {
            OnChunkUpdateEvent?.Invoke(properties);
        }
        internal void InvokeBlockUpdateEvent(OnBlockUpdateEventProperties properties)
        {
            OnBlockUpdateEvent?.Invoke(properties);
        }

    }
}