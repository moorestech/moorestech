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

        public CraftingInventoryViewPresenter(CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,PlayerInventoryViewModelController playerInventoryViewModelController,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _playerInventoryViewModelController = playerInventoryViewModelController;
            _playerInventoryViewModel = playerInventoryViewModel;
            craftingInventoryUpdateEvent.OnCraftingInventoryUpdate += UpdateInventory;
            craftingInventoryUpdateEvent.OnCraftingInventorySlotUpdate += UpdateSlotInventory;
        }

        public void UpdateInventory(CraftingInventoryUpdateProperties properties)
        {
            _playerInventoryViewModel.SetSubInventory(properties.ItemStacks);
            //イベントの発火
            for (int i = 0; i < properties.ItemStacks.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する
                var id = properties.ItemStacks[i].ID;
                var count = properties.ItemStacks[i].Count;
                _playerInventoryViewModelController.SetItemFromNetwork(PlayerInventoryConstant.MainInventorySize + i,id,count);
            }
        }

        public void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            var id = properties.ItemStack.ID;
            var count = properties.ItemStack.Count;
            
            _playerInventoryViewModelController.SetItemFromNetwork(PlayerInventoryConstant.MainInventorySize + slot,id,count);
        }

        public void Initialize() { }
    }
}