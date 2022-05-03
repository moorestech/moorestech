using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Control;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Receive
{
    public class BlockInventoryViewPresenter : IInitializable
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly PlayerInventoryViewModelController _playerInventoryViewModel;

        public BlockInventoryViewPresenter(BlockInventoryUpdateEvent blockInventoryUpdateEvent,PlayerInventoryViewModelController playerInventoryViewModel)
        {
            _playerInventoryViewModel = playerInventoryViewModel;
            blockInventoryUpdateEvent.OnSettingBlockInventory += SettingBlockInventory;
            blockInventoryUpdateEvent.OnBlockInventorySlotUpdate += BlockInventoryUpdate;
        }

        public void SettingBlockInventory(SettingBlockInventoryProperties properties)
        {
            //イベントの発火
            for (int i = 0; i < properties.ItemStacks.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する
                var id = properties.ItemStacks[i].ID;
                var count = properties.ItemStacks[i].Count;
                _playerInventoryViewModel.SetInventoryItem(PlayerInventoryConstant.MainInventorySize + i,id,count);
            }
        }

        public void BlockInventoryUpdate(BlockInventorySlotUpdateProperties properties)
        {
            var slot = properties.Slot;
            var id = properties.Id;
            var count = properties.Count;
            
            _playerInventoryViewModel.SetInventoryItem(PlayerInventoryConstant.MainInventorySize + slot,id,count);
        }

        public void Initialize() { }
    }
}