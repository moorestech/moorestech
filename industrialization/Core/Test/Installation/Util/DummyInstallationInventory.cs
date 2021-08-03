using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.Core.Item;
using industrialization.Core.Util;
using NUnit.Framework;

namespace industrialization.Core.Test.Installation
{
    public class DummyInstallationInventory : IInstallationInventory
    {
        public bool IsItemExists => _isItemExists;
        private bool _isItemExists = false;
        private readonly List<IItemStack> _insertedItems;
        
        public List<IItemStack> InsertedItems 
        {
            get
            {
                var a = _insertedItems.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
        private int InsertToEndNum { get; }
        private int _endInsertCnt;

        public DummyInstallationInventory(int insertToEndNum = 1)
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

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
        }
    }

    public class DummyInstallationInventoryTest
    {
        
        [Test]
        public void InsertItemTest()
        {
            var d = new DummyInstallationInventory();
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
            var d = new DummyInstallationInventory();
            d.ChangeConnector(null);
            Assert.True(true);
        }
    }
}