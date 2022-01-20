using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network.Interface;
using Maingame.Types;

namespace MainGame.GameLogic.Inventory
{
    public class InventoryDataStore
    {
        private List<ItemStack> _items = new List<ItemStack>();
        public InventoryDataStore(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            playerInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(OnPlayerInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
        }

        public void UpdateSlotInventory(OnPlayerInventorySlotUpdateProperties properties)
        {
            _items[properties.SlotId] = properties.ItemStack;
        }
    }
}