using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;

namespace Core.Inventory
{
    /// <summary>
    ///     開くことができるインベントリ（プレイヤーインベントリやチェストのインベントリ）の処理を統合的に行うクラスです
    /// </summary>
    public class OpenableInventoryItemDataStoreService : IOpenableInventory
    {
        public bool IsDestroy { get; private set; }
        
        public IReadOnlyList<IItemStack> Inventory => _inventory;
        public ReadOnlyCollection<IItemStack> Items => new(_inventory);
        public delegate void InventoryUpdate(int slot, IItemStack itemStack);

        private readonly List<IItemStack> _inventory;
        private readonly IItemStackFactory _itemStackFactory;
        private readonly InventoryUpdate _onInventoryUpdate;

        public OpenableInventoryItemDataStoreService(InventoryUpdate onInventoryUpdate, IItemStackFactory itemStackFactory, int slotNumber)
        {
            _itemStackFactory = itemStackFactory;
            _onInventoryUpdate = onInventoryUpdate;
            
            _inventory = new List<IItemStack>();
            for (var i = 0; i < slotNumber; i++) _inventory.Add(_itemStackFactory.CreatEmpty());
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            //インベントリのアイテムをコピー
            var inventoryCopy = new List<IItemStack>(_inventory);
            //挿入を実行する
            List<IItemStack> result = InventoryInsertItem.InsertItem(itemStacks, inventoryCopy, _itemStackFactory);
            //結果のアイテム数が0だったら挿入可能
            return result.Count == 0;
        }

        public int GetSlotSize()
        {
            return _inventory.Count;
        }

        public IItemStack GetItem(int slot)
        {
            return _inventory[slot];
        }
        
        private void InvokeEvent(int slot)
        {
            _onInventoryUpdate(slot, _inventory[slot]);
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

        public void SetItem(int slot, int itemId, int count)
        {
            SetItem(slot, _itemStackFactory.Create(itemId, count));
        }

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

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            return ReplaceItem(slot, _itemStackFactory.Create(itemId, count));
        }

        #endregion


        #region Insert

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return InventoryInsertItem.InsertItem(itemStack, _inventory, _itemStackFactory, InvokeEvent);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            return InsertItem(_itemStackFactory.Create(itemId, count));
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return InventoryInsertItem.InsertItem(itemStacks, _inventory, _itemStackFactory, InvokeEvent);
        }

        /// <summary>
        ///     もしそのスロットに入れられるのであれば、まずはそれらのスロットに入れてから、余ったものを返す
        /// </summary>
        public IItemStack InsertItemWithPrioritySlot(IItemStack itemStack, int[] prioritySlots)
        {
            return InventoryInsertItem.InsertItemWithPrioritySlot(itemStack, _inventory, _itemStackFactory, prioritySlots, InvokeEvent);
        }

        public IItemStack InsertItemWithPrioritySlot(int itemId, int count, int[] prioritySlots)
        {
            return InsertItemWithPrioritySlot(_itemStackFactory.Create(itemId, count), prioritySlots);
        }

        #endregion
    }
}