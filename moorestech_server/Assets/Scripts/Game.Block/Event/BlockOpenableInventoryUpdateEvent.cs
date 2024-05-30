using System;
using Game.Block.Interface.Event;

namespace Game.Block.Event
{
    public class BlockOpenableInventoryUpdateEvent : IBlockOpenableInventoryUpdateEvent
    {
        public void Subscribe(Action<BlockOpenableInventoryUpdateEventProperties> blockInventoryEvent)
        {
            OnBlockInventoryUpdate += blockInventoryEvent;
        }
        
        public event Action<BlockOpenableInventoryUpdateEventProperties> OnBlockInventoryUpdate;
        
        public void OnInventoryUpdateInvoke(BlockOpenableInventoryUpdateEventProperties properties)
        {
            OnBlockInventoryUpdate?.Invoke(properties);
        }
    }
}