using Game.Block.Interface.Event;
using static Game.Block.Interface.Event.IBlockOpenableInventoryUpdateEvent;

namespace Game.Block.Event
{
    public class BlockOpenableInventoryUpdateEvent : IBlockOpenableInventoryUpdateEvent
    {
        public void Subscribe(BlockInventoryEvent blockInventoryEvent)
        {
            OnBlockInventoryUpdate += blockInventoryEvent;
        }

        public event BlockInventoryEvent OnBlockInventoryUpdate;

        public void OnInventoryUpdateInvoke(
            BlockOpenableInventoryUpdateEventProperties properties)
        {
            OnBlockInventoryUpdate?.Invoke(properties);
        }
    }
}