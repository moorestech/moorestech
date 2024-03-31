using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item;
using Core.Item.Util;
using Game.Block.BlockInventory;

namespace Tests.Module
{
    public class DummyBlockInventory : IBlockInventory
    {
        private readonly List<IItemStack> _insertedItems;
        private readonly ItemStackFactory _itemStackFactory;
        private int _endInsertCnt;

        public DummyBlockInventory(ItemStackFactory itemStackFactory, int insertToEndNum = 1, int maxSlot = 100)
        {
            _itemStackFactory = itemStackFactory;
            IsItemExists = false;
            InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            _insertedItems = CreateEmptyItemStacksList.Create(maxSlot, _itemStackFactory).ToList();
        }

        public bool IsItemExists { get; private set; }

        public List<IItemStack> InsertedItems
        {
            get
            {
                var a = _insertedItems.Where(i => i.Id != ItemConst.EmptyItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        private int InsertToEndNum { get; }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _insertedItems.Count; i++)
            {
                if (!_insertedItems[i].IsAllowedToAdd(itemStack)) continue;
                var r = _insertedItems[i].AddItem(itemStack);
                _insertedItems[i] = r.ProcessResultItemStack;
                _endInsertCnt++;
                IsItemExists = InsertToEndNum <= _endInsertCnt;

                return r.RemainderItemStack;
            }

            return itemStack;
        }

        public IItemStack GetItem(int slot)
        {
            return _insertedItems[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _insertedItems[slot] = itemStack;
        }

        public int GetSlotSize()
        {
            return _insertedItems.Count;
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
        }
    }
}