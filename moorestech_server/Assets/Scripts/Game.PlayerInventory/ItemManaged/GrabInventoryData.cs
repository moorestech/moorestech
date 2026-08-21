using Core.Item.Interface;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class GrabInventoryData : PlayerOpenableInventoryDataBase
    {
        // 掴み枠は常に1枠
        // The grab inventory is always a single slot
        private const int GrabSlotCount = 1;

        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;

        public GrabInventoryData(int playerId, GrabInventoryUpdateEvent grabInventoryUpdateEvent) : base(playerId, GrabSlotCount)
        {
            _grabInventoryUpdateEvent = grabInventoryUpdateEvent;
        }

        public GrabInventoryData(int playerId, GrabInventoryUpdateEvent grabInventoryUpdateEvent, IItemStack itemStacks) : this(playerId, grabInventoryUpdateEvent)
        {
            OpenableInventoryService.SetItemWithoutEvent(0, itemStacks);
        }

        protected override void InvokeInventoryUpdateEvent(PlayerInventoryUpdateEventProperties properties)
        {
            _grabInventoryUpdateEvent.OnInventoryUpdateInvoke(properties);
        }
    }
}
