using Game.World.Interface.Event;

namespace World.Event
{
    public class BlockPlaceEvent : IBlockPlaceEvent
    {
        public event IBlockPlaceEvent.BlockPlaceEvent OnBlockPlaceEvent;

        public void Subscribe(IBlockPlaceEvent.BlockPlaceEvent blockPlaceEvent)
        {
            OnBlockPlaceEvent += blockPlaceEvent;
        }

        public void OnBlockPlaceEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            OnBlockPlaceEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}