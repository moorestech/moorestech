using Core.Item;
using Game.PlayerInventory.Interface;
using PlayerInventory.Event;

namespace PlayerInventory
{
    public class PlayerCraftingInventoryData : ICraftingInventory
    {
        //CraftInventoryはほとんどプレイヤーのインベントリと処理が同じなので、IInventoryの処理はPlayerInventoryDataに委譲しておく
        private readonly PlayerInventoryData _playerInventoryData;

        public PlayerCraftingInventoryData(int playerId, PlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            ItemStackFactory itemStackFactory)
        {
            _playerInventoryData = 
                new PlayerInventoryData(playerId, playerInventoryUpdateEvent, itemStackFactory,PlayerInventoryConst.CraftingInventorySize);
        }

        public void Craft()
        {
            throw new System.NotImplementedException();
        }

        public IItemStack GetResult()
        {
            throw new System.NotImplementedException();
        }


        #region delgate to PlayerInventoryData
        public IItemStack GetItem(int slot) { return _playerInventoryData.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _playerInventoryData.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _playerInventoryData.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _playerInventoryData.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _playerInventoryData.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _playerInventoryData.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _playerInventoryData.InsertItem(itemId, count); }
        public int GetSlotSize() { return _playerInventoryData.GetSlotSize(); }

        #endregion
    }
}