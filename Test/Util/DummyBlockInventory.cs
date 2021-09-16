using System.Collections.Generic;
using System.Linq;
using Core.Block;
using Core.Item;
using Core.Util;
using NUnit.Framework;

namespace industrialization_test.Util
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
                var a = _insertedItems.Where(i => i.Id != BlockConst.NullBlockId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
        private int InsertToEndNum { get; }
        private int _endInsertCnt;

        public DummyBlockInventory(int insertToEndNum = 1)
        {
            _isItemExists = false;
            this.InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            _insertedItems = CreateEmptyItemStacksList.Create(100).ToList();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _insertedItems.Count; i++)
            {
                if (!_insertedItems[i].IsAllowedToAdd(itemStack)) continue;
                var r = _insertedItems[i].AddItem(itemStack);
                _insertedItems[i] = r.MineItemStack;
                _endInsertCnt++;
                _isItemExists = InsertToEndNum <= _endInsertCnt;

                return r.ReceiveItemStack;
            }
            return new NullItemStack();
        }

        public void ChangeConnector(IBlockInventory blockInventory)
        {
        }
    }

    public class DummyBlockInventoryTest
    {
        
        [Test]
        public void InsertItemTest()
        {
            var d = new DummyBlockInventory();
            for (int i = 1; i <= 100; i++)
            {
                d.InsertItem(new ItemStack(i,1));
            }
            
            var item = d.InsertItem(new ItemStack(101,1));
            Assert.True(item.Equals(new NullItemStack()));
        }

        [Test]
        public void ChangeConnectorTest()
        {
            var d = new DummyBlockInventory();
            d.ChangeConnector(null);
            Assert.True(true);
        }
    }
}