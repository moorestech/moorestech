using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.UnityView.UI.Inventory.Control;

namespace MainGame.Presenter.Inventory
{
    public class CraftingInventoryDataCache
    {
        private readonly PlayerInventoryViewModelController _playerInventoryViewModel;

        public CraftingInventoryDataCache(CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,PlayerInventoryViewModelController playerInventoryViewModel)
        {
            _playerInventoryViewModel = playerInventoryViewModel;
            craftingInventoryUpdateEvent.OnCraftingInventoryUpdate += UpdateInventory;
            craftingInventoryUpdateEvent.OnCraftingInventorySlotUpdate += UpdateSlotInventory;
        }

        public void UpdateInventory(CraftingInventoryUpdateProperties properties)
        {
            //イベントの発火
            for (int i = 0; i < properties.ItemStacks.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する
                var id = properties.ItemStacks[i].ID;
                var count = properties.ItemStacks[i].Count;
                _playerInventoryViewModel.SetItem(PlayerInventoryConstant.MainInventorySize + i,id,count);
            }
        }

        public void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            var id = properties.ItemStack.ID;
            var count = properties.ItemStack.Count;
            
            _playerInventoryViewModel.SetItem(PlayerInventoryConstant.MainInventorySize + slot,id,count);
        }

        public void Initialize() { }
    }
}