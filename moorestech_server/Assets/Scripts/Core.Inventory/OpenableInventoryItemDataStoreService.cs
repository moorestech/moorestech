using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Core.Master;

namespace Core.Inventory
{
    /// <summary>
    ///     開くことができるインベントリ（プレイヤーインベントリやチェストのインベントリ）の処理を統合的に行うクラスです
    /// </summary>
    public class OpenableInventoryItemDataStoreService : IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _inventory;
        public IReadOnlyList<int> NonEmptySlotIndexes => _slotIndex.NonEmptySlotIndexes;
        public bool HasInsertableSlot => _slotIndex.HasInsertableSlot;
        public int InventoryVersion => _slotIndex.Version;

        private readonly List<IItemStack> _inventory;
        private readonly OpenableInventorySlotIndex _slotIndex;

        public delegate void InventoryUpdate(int slot, IItemStack itemStack);

        private readonly IItemStackFactory _itemStackFactory;
        private readonly InventoryUpdate _onInventoryUpdate;
        private readonly OpenableInventoryItemDataStoreServiceOption _option;

        public OpenableInventoryItemDataStoreService(InventoryUpdate onInventoryUpdate, IItemStackFactory itemStackFactory, int slotNumber, OpenableInventoryItemDataStoreServiceOption option = null)
        {
            _itemStackFactory = itemStackFactory;
            _onInventoryUpdate = onInventoryUpdate;
            _option = option ?? new OpenableInventoryItemDataStoreServiceOption();

            _inventory = new List<IItemStack>();
            for (var i = 0; i < slotNumber; i++) _inventory.Add(_itemStackFactory.CreatEmpty());
            _slotIndex = new OpenableInventorySlotIndex(slotNumber);
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            //インベントリのアイテムをコピー
            var inventoryCopy = new List<IItemStack>(_inventory);
            //挿入を実行する
            var result = InventoryInsertItem.InsertItem(itemStacks, inventoryCopy, _itemStackFactory, _option);
            //結果のアイテム数が0だったら挿入可能
            return result.Count == 0;
        }
        
        public int GetSlotSize()
        {
            return _inventory.Count;
        }
        
        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return new(_inventory);
        }
        
        public IItemStack GetItem(int slot)
        {
            return _inventory[slot];
        }

        public bool CanInsertItem(IItemStack itemStack)
        {
            return _slotIndex.CanInsertItem(itemStack, _option);
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
                var oldItemStack = _inventory[slot];
                _inventory[slot] = itemStack;
                _slotIndex.SetSlot(slot, oldItemStack, itemStack);
                InvokeEvent(slot);
            }
        }
        
        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            var oldItemStack = _inventory[slot];
            _inventory[slot] = itemStack;
            _slotIndex.SetSlot(slot, oldItemStack, itemStack);
        }
        
        public void SetItem(int slot, ItemId itemId, int count)
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
                _slotIndex.SetSlot(slot, item, result.ProcessResultItemStack);
                InvokeEvent(slot);
                return result.RemainderItemStack;
            }
            
            //違う場合はそのまま入れ替える
            _inventory[slot] = itemStack;
            _slotIndex.SetSlot(slot, item, itemStack);
            InvokeEvent(slot);
            return item;
        }
        
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            return ReplaceItem(slot, _itemStackFactory.Create(itemId, count));
        }
        
        #endregion
        
        
        #region Insert
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var remainingItem = InventoryInsertItem.InsertItem(itemStack, _inventory, _itemStackFactory, _option, InvokeEvent);
            _slotIndex.Rebuild(_inventory);
            return remainingItem;
        }

        public IItemStack InsertItemByIndex(IItemStack itemStack)
        {
            return _slotIndex.InsertItem(itemStack, _inventory, _itemStackFactory, _option, InvokeEvent);
        }
        
        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return InsertItem(_itemStackFactory.Create(itemId, count));
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var remainingItems = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                var remaining = InsertItem(itemStack);
                if (remaining.Count == 0) continue;
                remainingItems.Add(remaining);
            }
            return remainingItems;
        }
        
        /// <summary>
        ///     もしそのスロットに入れられるのであれば、まずはそれらのスロットに入れてから、余ったものを返す
        /// </summary>
        public IItemStack InsertItemWithPrioritySlot(IItemStack itemStack, int[] prioritySlots)
        {
            var remainingItem = InventoryInsertItem.InsertItemWithPrioritySlot(itemStack, _inventory, _itemStackFactory, _option, prioritySlots, InvokeEvent);
            _slotIndex.Rebuild(_inventory);
            return remainingItem;
        }
        
        public IItemStack InsertItemWithPrioritySlot(ItemId itemId, int count, int[] prioritySlots)
        {
            return InsertItemWithPrioritySlot(_itemStackFactory.Create(itemId, count), prioritySlots);
        }
        
        #endregion
    }
    
    public class OpenableInventoryItemDataStoreServiceOption
    {
        /// <summary>
        /// インサート時に、同一アイテムについて既存スタックとは別に、新しいスタックの生成を許可するかどうかを示します。
        /// If true, inserting an item may create a new stack  even if another stack of the same item already exists.
        /// </summary>
        public bool AllowMultipleStacksPerItemOnInsert { get; set; } = true;
    }
}
