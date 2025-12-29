using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Item.Util;
using Game.Block.Interface.Component;

namespace Tests.Module
{
    public class ConfigurableBlockInventory : IBlockInventory
    {
        private readonly List<IItemStack> _slotItems;
        private readonly List<IItemStack> _insertedItems;
        private bool _allowInsertionCheck;
        private bool _rejectInsert;
        private int _maxInsertCount;

        public ConfigurableBlockInventory(int maxSlot, int maxInsertCount, bool allowInsertionCheck, bool rejectInsert)
        {
            _slotItems = CreateEmptyItemStacksList.Create(maxSlot).ToList();
            _insertedItems = new List<IItemStack>();
            _allowInsertionCheck = allowInsertionCheck;
            _rejectInsert = rejectInsert;
            _maxInsertCount = maxInsertCount;
        }

        public void SetAllowInsertionCheck(bool allowInsertionCheck)
        {
            _allowInsertionCheck = allowInsertionCheck;
        }

        public void SetRejectInsert(bool rejectInsert)
        {
            _rejectInsert = rejectInsert;
        }

        public void SetMaxInsertCount(int maxInsertCount)
        {
            _maxInsertCount = maxInsertCount;
        }

        public int GetInsertedItemCount()
        {
            return _insertedItems.Count;
        }

        public IReadOnlyList<IItemStack> GetInsertedItems()
        {
            return _insertedItems;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            // 受け入れ可否と容量を判定する
            // Validate acceptability and capacity
            if (_rejectInsert) return itemStack;
            if (_insertedItems.Count >= _maxInsertCount) return itemStack;
            _insertedItems.Add(itemStack);
            return itemStack.SubItem(itemStack.Count);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            // 挿入可能かどうかを入力条件で判定する
            // Check insertability based on input conditions
            if (!_allowInsertionCheck) return false;
            if (itemStacks.Count != 1 || itemStacks[0].Count != 1) return false;
            return _insertedItems.Count < _maxInsertCount;
        }

        public IItemStack GetItem(int slot)
        {
            return _slotItems[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _slotItems[slot] = itemStack;
        }

        public int GetSlotSize()
        {
            return _slotItems.Count;
        }

        public bool IsDestroy => false;

        public void Destroy()
        {
        }
    }
}
