using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class PlayerInventoryItemDataStoreService : IInventory
    {
        private readonly int _playerId;
        private readonly List<IItemStack> _mainInventory;
        private readonly IPlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryItemDataStoreService(int playerId, IPlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,int slotNumber)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;

            _playerId = playerId;
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < slotNumber; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
        }

        #region Set

        public void SetItem(int slot, IItemStack itemStack)
        {
            _mainInventory[slot] = itemStack;
            InvokeEvent(slot);
        }
        public void SetItem(int slot, int itemId, int count) { SetItem(slot, _itemStackFactory.Create(itemId, count)); }
        
        #endregion

        
        #region Replace
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            //アイテムIDが同じの時はスタックして余ったものを返す
            var item = _mainInventory[slot];
            if (item.Id == itemStack.Id)
            {
                var result = item.AddItem(itemStack);
                _mainInventory[slot] = result.ProcessResultItemStack;
                _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                    new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            _mainInventory[slot] = itemStack;
            InvokeEvent(slot);
            return item;
        }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return ReplaceItem(slot, _itemStackFactory.Create(itemId, count)); }

        #endregion


        #region Insert
        
        public IItemStack InsertItem(int slot, IItemStack itemStack)
        {
            if (!_mainInventory[slot].IsAllowedToAdd(itemStack)) return itemStack;
            
            var result = _mainInventory[slot].AddItem(itemStack);
            _mainInventory[slot] = result.ProcessResultItemStack;

            InvokeEvent(slot);
            
            return result.RemainderItemStack;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _mainInventory.Count; i++)
            {
                //挿入できるスロットを探索
                if (!_mainInventory[i].IsAllowedToAdd(itemStack)) continue;
                //挿入実行
                return InsertItem(i,itemStack);
            }
            return itemStack;
        }
        public IItemStack InsertItem(int itemId, int count) { return InsertItem(_itemStackFactory.Create(itemId, count)); }

        #endregion
        
        
        
        
        public int GetSlotSize() { return _mainInventory.Count; }
        public IItemStack GetItem(int slot) { return _mainInventory[slot]; }


        private void InvokeEvent(int slot)
        {
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
        }
        
        
    }
}