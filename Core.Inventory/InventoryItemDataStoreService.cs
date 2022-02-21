using System.Collections.Generic;
using Core.Item;

namespace Core.Inventory
{
    public class InventoryItemDataStoreService : IInventory
    {
        public delegate void OnInventoryUpdate(int slot, IItemStack itemStack);
        
        private readonly OnInventoryUpdate _onInventoryUpdate;
        private readonly List<IItemStack> _inventory;
        public IReadOnlyList<IItemStack> Inventory => _inventory;

        private readonly ItemStackFactory _itemStackFactory;

        public InventoryItemDataStoreService(OnInventoryUpdate onInventoryUpdate,
            ItemStackFactory itemStackFactory,int slotNumber)
        {
            _itemStackFactory = itemStackFactory;

            _onInventoryUpdate = onInventoryUpdate;
            _inventory = new List<IItemStack>();
            for (int i = 0; i < slotNumber; i++)
            {
                _inventory.Add(_itemStackFactory.CreatEmpty());
            }
        }

        #region Set

        public void SetItem(int slot, IItemStack itemStack)
        {
            _inventory[slot] = itemStack;
            InvokeEvent(slot);
        }
        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _inventory[slot] = itemStack;
        }
        public void SetItem(int slot, int itemId, int count) { SetItem(slot, _itemStackFactory.Create(itemId, count)); }
        
        #endregion

        
        #region Replace
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            //アイテムIDが同じの時はスタックして余ったものを返す
            var item = _inventory[slot];
            if (item.Id == itemStack.Id)
            {
                var result = item.AddItem(itemStack);
                _inventory[slot] = result.ProcessResultItemStack;
                InvokeEvent(slot);
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            _inventory[slot] = itemStack;
            InvokeEvent(slot);
            return item;
        }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return ReplaceItem(slot, _itemStackFactory.Create(itemId, count)); }

        #endregion


        #region Insert
        
        public IItemStack InsertItem(int slot, IItemStack itemStack)
        {
            if (!_inventory[slot].IsAllowedToAddButRemain(itemStack)) return itemStack;
            
            var result = _inventory[slot].AddItem(itemStack);
            _inventory[slot] = result.ProcessResultItemStack;

            InvokeEvent(slot);
            
            return result.RemainderItemStack;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _inventory.Count; i++)
            {
                //挿入できるスロットを探索
                if (!_inventory[i].IsAllowedToAddButRemain(itemStack)) continue;
                
                //挿入実行
                var remain = InsertItem(i, itemStack);
                
                //挿入結果が空のアイテムならそのまま処理を終了
                if (remain.Equals(_itemStackFactory.CreatEmpty()))
                {
                    return remain;
                }
                //そうでないならあまりのアイテムを入れるまで探索
                itemStack = remain;
            }
            return itemStack;
        }
        public IItemStack InsertItem(int itemId, int count) { return InsertItem(_itemStackFactory.Create(itemId, count)); }

        #endregion
        
        
        
        
        public int GetSlotSize() { return _inventory.Count; }
        public IItemStack GetItem(int slot) { return _inventory[slot]; }


        private void InvokeEvent(int slot)
        {
            _onInventoryUpdate(slot, _inventory[slot]);
        }
        
        
    }
}