using Core.Block;

namespace World.Event
{
    public class BlockPlaceEvent
    {
        public delegate void PutBlockEvent(BlockPlaceEventProperties blockPlaceEventProperties);
        private event PutBlockEvent OnBlockPutEvent;

        public void Subscribe(PutBlockEvent @event) { OnBlockPutEvent += @event; }
        public void UnSubscribe(PutBlockEvent @event) { OnBlockPutEvent -= @event; }

        internal void OnBlockPutEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            OnBlockPutEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}