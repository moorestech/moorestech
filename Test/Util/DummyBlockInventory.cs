using System.Collections.Generic;
using System.Linq;
using Core.Block;
using Core.Block.BlockInventory;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using Core.Util;
using NUnit.Framework;

namespace Test.Util
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

        public DummyBlockInventory(int insertToEndNum = 1)
        {
            _itemStackFactory = new ItemStackFactory(new TestItemConfig());
            _isItemExists = false;
            this.InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            _insertedItems = CreateEmptyItemStacksList.Create(100,_itemStackFactory).ToList();
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

        public void AddConnector(IBlockInventory blockInventory) { }
        public void RemoveConnector(IBlockInventory blockInventory) { }
    }

    public class DummyBlockInventoryTest
    {
        
        [Test]
        public void InsertItemTest()
        {
            var _itemStackFactory = new ItemStackFactory(new TestItemConfig());
            var d = new DummyBlockInventory();
            for (int i = 1; i <= 100; i++)
            {
                d.InsertItem(_itemStackFactory.Create(i,1));
            }
            
            var item = d.InsertItem(_itemStackFactory.Create(101,1));
            Assert.True(item.Equals(_itemStackFactory.CreatEmpty()));
        }

        [Test]
        public void ChangeConnectorTest()
        {
            var d = new DummyBlockInventory();
            d.AddConnector(null);
            d.RemoveConnector(null);
            Assert.True(true);
        }
    }
}