using System;
using Game.World.Interface.Event;

namespace Game.World.Event
{
    [Obsolete("TODO 後で消す")]
    public class BlockRemoveEvent : IBlockRemoveEvent
    {
        public void Subscribe(IBlockRemoveEvent.BlockPlaceEvent blockRemoveEvent)
        {
            OnBlockRemoveEvent += blockRemoveEvent;
        }

        public event IBlockRemoveEvent.BlockPlaceEvent OnBlockRemoveEvent;

        public void OnBlockRemoveEventInvoke(BlockRemoveEventProperties blockPlaceEventProperties)
        {
            OnBlockRemoveEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}