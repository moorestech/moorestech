using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Control;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Receive
{
    public class CraftingInventoryViewPresenter : IInitializable
    {
        private readonly PlayerInventoryViewModelController _playerInventoryViewModelController;
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;

        public CraftingInventoryViewPresenter(ReciveCraftingInventoryEvent reciveCraftingInventoryEvent,PlayerInventoryViewModelController playerInventoryViewModelController,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _playerInventoryViewModelController = playerInventoryViewModelController;
            _playerInventoryViewModel = playerInventoryViewModel;
            reciveCraftingInventoryEvent.OnCraftingInventoryUpdate += UpdateInventory;
            reciveCraftingInventoryEvent.OnCraftingInventorySlotUpdate += UpdateSlotInventory;
        }

        public void UpdateInventory(CraftingInventoryUpdateProperties properties)
        {
            //サブインベントリの内容を設定する
            var subInventory = new List<ItemStack>();
            subInventory.AddRange(properties.ItemStacks);
            subInventory.Add(properties.ResultItemStack);
            
            _playerInventoryViewModel.SetSubInventory(subInventory);
            
            
            //イベントの発火
            for (int i = 0; i < properties.ItemStacks.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する
                var id = properties.ItemStacks[i].ID;
                var count = properties.ItemStacks[i].Count;
                _playerInventoryViewModelController.SetInventoryItem(PlayerInventoryConstant.MainInventorySize + i,id,count);
            }
            //クラフト結果のアイテムを更新する
            _playerInventoryViewModelController.SetInventoryItem(
                PlayerInventoryConstant.MainInventorySize + PlayerInventoryConstant.CraftingSlotSize,properties.ResultItemStack.ID,properties.ResultItemStack.Count);
        }

        public void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            var id = properties.ItemStack.ID;
            var count = properties.ItemStack.Count;
            
            //更新対象のインベントリにアイテムを設定
            _playerInventoryViewModelController.SetInventoryItem(PlayerInventoryConstant.MainInventorySize + slot,id,count);
            //結果スロットにアイテムを設定
            _playerInventoryViewModelController.SetInventoryItem(
                PlayerInventoryConstant.MainInventorySize + PlayerInventoryConstant.CraftingSlotSize,properties.ResultItemStack.ID,properties.ResultItemStack.Count);
        }

        public void Initialize() { }
    }
}