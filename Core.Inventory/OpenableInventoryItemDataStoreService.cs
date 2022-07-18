using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item;

namespace Core.Inventory
{
    public class OpenableInventoryItemDataStoreService : IOpenableInventory
    {
        public delegate void OnInventoryUpdate(int slot, IItemStack itemStack);
        
        private readonly OnInventoryUpdate _onInventoryUpdate;
        private readonly List<IItemStack> _inventory;
        public IReadOnlyList<IItemStack> Inventory => _inventory;

        private readonly ItemStackFactory _itemStackFactory;

        public OpenableInventoryItemDataStoreService(OnInventoryUpdate onInventoryUpdate,
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
            if (!_inventory[slot].Equals(itemStack))
            {
                _inventory[slot] = itemStack;
                InvokeEvent(slot);
            }
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
            if (itemStack.Equals(_itemStackFactory.CreatEmpty())) return itemStack;
            if (!_inventory[slot].IsAllowedToAddWithRemain(itemStack)) return itemStack;
            
            var result = _inventory[slot].AddItem(itemStack);

            //挿入を試した結果が今までと違う場合は入れ替えをしてイベントを発火
            if (!_inventory[slot].Equals(result.ProcessResultItemStack))
            {
                _inventory[slot] = result.ProcessResultItemStack;
                InvokeEvent(slot);
            }
            
            return result.RemainderItemStack;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _inventory.Count; i++)
            {
                //挿入できるスロットを探索
                if (!_inventory[i].IsAllowedToAddWithRemain(itemStack)) continue;
                
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
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var reminderItemStacks = new List<IItemStack>();
            
            foreach (var item in itemStacks)
            {
                var remindItemStack = InsertItem(item);
                if (remindItemStack.Equals(_itemStackFactory.CreatEmpty())) continue;
                
                reminderItemStacks.Add(remindItemStack);
            }

            return reminderItemStacks;
        }


        #endregion
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            throw new System.NotImplementedException();
        }
        
        
        
        
        public ReadOnlyCollection<IItemStack> Items => new(_inventory);
        public int GetSlotSize() { return _inventory.Count; }
        public IItemStack GetItem(int slot) { return _inventory[slot]; }


        private void InvokeEvent(int slot)
        {
            _onInventoryUpdate(slot, _inventory[slot]);
        }
        
        
    }
}