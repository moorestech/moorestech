using Game.Block.Interface.Event;
using static Game.Block.Interface.Event.IBlockOpenableInventoryUpdateEvent;

namespace Game.Block.Event
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