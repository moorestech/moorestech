using Core.Block;
using Game.World.Interface.Event;

namespace World.Event
{
    public class BlockPlaceEvent : IBlockPlaceEvent
    {
        public event IBlockPlaceEvent.BlockPlaceEvent OnBlockPutEvent;

        public void Subscribe(IBlockPlaceEvent.BlockPlaceEvent blockPlaceEvent)
        {
            OnBlockPutEvent += blockPlaceEvent;
        }

        public void OnBlockPutEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            OnBlockPutEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}