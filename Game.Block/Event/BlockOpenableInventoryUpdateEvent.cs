using static Core.Block.Event.IBlockOpenableInventoryUpdateEvent;

namespace Core.Block.Event
{
    public class BlockOpenableInventoryUpdateEvent : IBlockOpenableInventoryUpdateEvent
    {
        public event BlockInventoryEvent OnBlockInventoryUpdate;
        public void Subscribe(BlockInventoryEvent blockInventoryEvent)
        {
            OnBlockInventoryUpdate += blockInventoryEvent;
        }
        public void OnInventoryUpdateInvoke(
            BlockOpenableInventoryUpdateEventProperties properties)
        {
            OnBlockInventoryUpdate?.Invoke(properties);
        }
    }
}