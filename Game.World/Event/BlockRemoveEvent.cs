using Game.World.Interface.Event;

namespace World.Event
{
    
    public class BlockRemoveEvent : IBlockRemoveEvent
    {
        public event IBlockRemoveEvent.BlockPlaceEvent OnBlockRemoveEvent;

        public void Subscribe(IBlockRemoveEvent.BlockPlaceEvent blockRemoveEvent)
        {
            OnBlockRemoveEvent += blockRemoveEvent;
        }

        public void OnBlockRemoveEventInvoke(BlockRemoveEventProperties blockPlaceEventProperties)
        {
            OnBlockRemoveEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}