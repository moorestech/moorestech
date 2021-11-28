using Core.Block;

namespace World.Event
{
    public class BlockPlaceEvent
    {
        public delegate void PutBlockEvent(BlockPlaceEventProperties blockPlaceEventProperties);
        public event PutBlockEvent OnBlockPutEvent;

        internal void OnBlockPutEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            OnBlockPutEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}