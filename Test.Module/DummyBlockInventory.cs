using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockInventory;
using Core.Item;
using Core.Item.Config;
using Core.Item.Util;

namespace Test.Module
{
    public class DummyBlockInventory : IBlockInventory
    {
        public bool IsItemExists => _isItemExists;
        private bool _isItemExists = false;
        private readonly List<IItemStack> _insertedItems;

        public List<IItemStack> InsertedItems
        {
            get
            {
                var a = _insertedItems.Where(i => i.Id != ItemConst.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        private int InsertToEndNum { get; }
        private int _endInsertCnt;
        private ItemStackFactory _itemStackFactory;

        public DummyBlockInventory(int insertToEndNum = 1,int maxSlot = 100)
        {
            _itemStackFactory = new ItemStackFactory(new TestItemConfig());
            _isItemExists = false;
            this.InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            _insertedItems = CreateEmptyItemStacksList.Create(maxSlot, _itemStackFactory).ToList();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _insertedItems.Count; i++)
            {
                if (!_insertedItems[i].IsAllowedToAdd(itemStack)) continue;
                var r = _insertedItems[i].AddItem(itemStack);
                _insertedItems[i] = r.ProcessResultItemStack;
                _endInsertCnt++;
                _isItemExists = InsertToEndNum <= _endInsertCnt;

                return r.RemainderItemStack;
            }

            return _itemStackFactory.CreatEmpty();
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
        }
    }
}