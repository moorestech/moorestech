using System;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ItemStackTest
    {
        private const int EmptyItemId = 0;
        private IItemStackFactory _itemStackFactory;
        
        [SetUp]
        public void Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            _itemStackFactory = ServerContext.ItemStackFactory;
        }
        
        [TestCase(1, 1, 1, 1, 2, 0, 1, EmptyItemId)]
        [TestCase(1, 5, 1, 1, 6, 0, 1, EmptyItemId)]
        [TestCase(EmptyItemId, 0, 1, 3, 3, 0, 1, EmptyItemId)]
        [TestCase(EmptyItemId, 0, 2, 9, 9, 0, 2, EmptyItemId)]
        [TestCase(EmptyItemId, 5, 1, 1, 1, 0, 1, EmptyItemId)]
        [TestCase(1, 1, EmptyItemId, 0, 1, 0, 1, EmptyItemId)]
        [TestCase(1, 1, EmptyItemId, 0, 1, 0, 1, EmptyItemId)]
        [TestCase(1, 5, EmptyItemId, 0, 5, 0, 1, EmptyItemId)]
        [TestCase(3, 1, 1, 8, 1, 8, 3, 1)]
        [TestCase(1, 1, 3, 1, 1, 1, 1, 3)]
        [TestCase(2, 5, 5, 3, 5, 3, 2, 5)]
        public void AddTest(int mid, int mamo, int rid, int ramo, int ansMAmo, int ansRAmo, int ansMid, int ansRID)
        {
            IItemStack mineItemStack;
            if (mid == EmptyItemId)
                mineItemStack = _itemStackFactory.CreatEmpty();
            else
                mineItemStack = _itemStackFactory.Create(new ItemId(mid), mamo);
            
            IItemStack receivedItemStack;
            if (rid == EmptyItemId)
                receivedItemStack = _itemStackFactory.CreatEmpty();
            else
                receivedItemStack = _itemStackFactory.Create(new ItemId(rid), ramo);
            
            var result = mineItemStack.AddItem(receivedItemStack);
            Assert.AreEqual(result.ProcessResultItemStack.Count, ansMAmo);
            Assert.AreEqual(result.RemainderItemStack.Count, ansRAmo);
            Assert.AreEqual(result.ProcessResultItemStack.Id, ansMid);
            Assert.AreEqual(result.RemainderItemStack.Id, ansRID);
        }
        
        [TestCase(1, 5, 1, 4, 1)]
        [TestCase(EmptyItemId, 5, 1, 0, EmptyItemId)]
        [TestCase(1, 5, 10, 0, EmptyItemId)]
        [TestCase(1, 8, 8, 0, EmptyItemId)]
        [TestCase(1, 8, 9, 0, EmptyItemId)]
        public void SubTest(int mid, int mamo, int subamo, int ansamo, int ansID)
        {
            IItemStack mineItemStack;
            if (mid == EmptyItemId)
                mineItemStack = _itemStackFactory.CreatEmpty();
            else
                mineItemStack = _itemStackFactory.Create(new ItemId(mid), mamo);
            
            var result = mineItemStack.SubItem(subamo);
            Assert.AreEqual(ansamo, result.Count);
            Assert.AreEqual(ansID, result.Id.AsPrimitive());
        }
        
        
        [TestCase(3, 299, 0, 0)]
        [TestCase(3, 299, 1, 0)]
        [TestCase(3, 150, 150, 0)]
        [TestCase(3, 300, 1, 1)]
        [TestCase(3, 1, 300, 1)]
        [TestCase(3, 300, 300, 300)]
        public void ItemAddToOverFlowTest(int id, int baseAmount, int addAmount, int overflowAmount)
        {
            var baseItem = _itemStackFactory.Create(new ItemId(id), baseAmount);
            
            
            var result = baseItem.AddItem(_itemStackFactory.Create(new ItemId(id), addAmount));
            Assert.True(_itemStackFactory.Create(new ItemId(id), overflowAmount).Equals(result.RemainderItemStack));
        }
        
        [TestCase(1, 100, false)]
        [TestCase(1, 1001, true)]
        [TestCase(1, 200, true)]
        public void ItemAddToOverFlowThrowTest(int id, int baseAmo, bool isthrow)
        {
            try
            {
                _itemStackFactory.Create(new ItemId(id), baseAmo);
                Assert.False(isthrow);
            }
            catch (Exception e)
            {
                Assert.True(isthrow);
            }
        }
        
        
        //関係ないオブジェクトを渡すFalseになるテスト
        [TestCase(0)]
        [TestCase(1.5)]
        [TestCase("aaa")]
        public void NotRelatedObjectPassFalseHaveTest(object obj)
        {
            var nullItem = _itemStackFactory.CreatEmpty();
            Assert.False(nullItem.Equals(obj));
            var item = _itemStackFactory.Create(new ItemId(5), 1);
            Assert.False(item.Equals(obj));
        }
        
        [Test]
        public void ToStringTest()
        {
            var item = _itemStackFactory.CreatEmpty();
            Assert.True(item.ToString() == "ID:0 Count:0");
            item = _itemStackFactory.Create(new ItemId(1), 5);
            Assert.True(item.ToString() == "ID:1 Count:5");
            item = _itemStackFactory.Create(new ItemId(13), 10);
            Assert.True(item.ToString() == "ID:13 Count:10");
        }
    }
}