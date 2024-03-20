using System;
using Game.World.Interface.Event;

namespace Game.World.Event
{
    [Obsolete("TODO 後で消す")]
    public class BlockPlaceEvent : IBlockPlaceEvent
    {
        public void Subscribe(IBlockPlaceEvent.BlockPlaceEvent blockPlaceEvent)
        {
            OnBlockPlaceEvent += blockPlaceEvent;
        }

        public event IBlockPlaceEvent.BlockPlaceEvent OnBlockPlaceEvent;

        public void OnBlockPlaceEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            OnBlockPlaceEvent?.Invoke(blockPlaceEventProperties);
        }
    }
}