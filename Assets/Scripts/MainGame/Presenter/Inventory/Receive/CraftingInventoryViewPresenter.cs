using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
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
        
        private const int MainSize = PlayerInventoryConst.MainInventorySize;
        private const int CraftSize = PlayerInventoryConst.CraftingSlotSize;

        public CraftingInventoryViewPresenter(ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent,PlayerInventoryViewModelController playerInventoryViewModelController,PlayerInventoryViewModel playerInventoryViewModel)
        {
            
            _playerInventoryViewModelController = playerInventoryViewModelController;
            _playerInventoryViewModel = playerInventoryViewModel;
            receiveCraftingInventoryEvent.OnCraftingInventoryUpdate += UpdateInventory;
            receiveCraftingInventoryEvent.OnCraftingInventorySlotUpdate += UpdateSlotInventory;
        }

        private void UpdateInventory(CraftingInventoryUpdateProperties properties)
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
                SetInventory(MainSize+ i,properties.ItemStacks[i]);
            }
            //クラフト結果のアイテムを更新する
            SetInventory(MainSize + CraftSize,properties.ResultItemStack);
        }

        private void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            
            //更新対象のインベントリにアイテムを設定
            SetInventory(MainSize + slot,properties.ItemStack);
            //結果スロットにアイテムを設定
            SetInventory(MainSize + PlayerInventoryConst.CraftingSlotSize,properties.ResultItemStack);
        }
        
        
        private void SetInventory(int slot, ItemStack itemStack) { _playerInventoryViewModelController.SetInventoryItem(slot,itemStack.ID,itemStack.Count); }
        public void Initialize() { }
    }
}