using System.Collections.Generic;
using Core.Item.Interface;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class MainOpenableInventoryData : PlayerOpenableInventoryDataBase
    {
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;

        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, int slotCount) : base(playerId, slotCount)
        {
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent;
        }

        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, int slotCount, List<IItemStack> itemStacks) : this(playerId, mainInventoryUpdateEvent, slotCount)
        {
            for (var i = 0; i < itemStacks.Count; i++) OpenableInventoryService.SetItemWithoutEvent(i, itemStacks[i]);
        }

        // 所持枠だけが研究で拡張される
        // Only the main inventory grows through research
        public void ExpandSlots(int newSlotCount)
        {
            OpenableInventoryService.ExpandSlots(newSlotCount);
        }

        protected override void InvokeInventoryUpdateEvent(PlayerInventoryUpdateEventProperties properties)
        {
            _mainInventoryUpdateEvent.OnInventoryUpdateInvoke(properties);
        }
    }
}
