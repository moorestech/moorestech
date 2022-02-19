using System;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Test.Module;

namespace Test.UnitTest.Core.Other
{
    [TestClass]
    public class ItemStackTest
    {
        private ItemStackFactory _itemStackFactory;

        [TestInitialize]
        public void Setup()
        {
            _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        }

        public void AddTest()
        {
            AddTest(1, 1, 1, 1, 2, 0, 1, ItemConst.EmptyItemId);
            AddTest(1, 5, 1, 1, 6, 0, 1, ItemConst.EmptyItemId);
            AddTest(ItemConst.EmptyItemId, 0, 1, 3, 3, 0, 1, ItemConst.EmptyItemId);
            AddTest(ItemConst.EmptyItemId, 0, 2, 9, 9, 0, 2, ItemConst.EmptyItemId);
            AddTest(ItemConst.EmptyItemId, 5, 1, 1, 1, 0, 1, ItemConst.EmptyItemId);
            AddTest(1, 1, ItemConst.EmptyItemId, 0, 1, 0, 1, ItemConst.EmptyItemId);
            AddTest(1, 1, ItemConst.EmptyItemId, 0, 1, 0, 1, ItemConst.EmptyItemId);
            AddTest(1, 5, ItemConst.EmptyItemId, 0, 5, 0, 1, ItemConst.EmptyItemId);
            AddTest(3, 1, 1, 8, 1, 8, 3, 1);
            AddTest(1, 1, 3, 1, 1, 1, 1, 3);
            AddTest(2, 5, 5, 3, 5, 3, 2, 5);
        }
        public void AddTest(int mid, int mamo, int rid, int ramo, int ansMAmo, int ansRAmo, int ansMid, int ansRID)
        {
            IItemStack mineItemStack;
            if (mid == ItemConst.EmptyItemId)
            {
                mineItemStack = _itemStackFactory.CreatEmpty();
            }
            else
            {
                mineItemStack = _itemStackFactory.Create(mid, mamo);
            }

            IItemStack receivedItemStack;
            if (rid == ItemConst.EmptyItemId)
            {
                receivedItemStack = _itemStackFactory.CreatEmpty();
            }
            else
            {
                receivedItemStack = _itemStackFactory.Create(rid, ramo);
            }

            var result = mineItemStack.AddItem(receivedItemStack);
            Assert.AreEqual(result.ProcessResultItemStack.Count, ansMAmo);
            Assert.AreEqual(result.RemainderItemStack.Count, ansRAmo);
            Assert.AreEqual(result.ProcessResultItemStack.Id, ansMid);
            Assert.AreEqual(result.RemainderItemStack.Id, ansRID);
        }

        public void SubTest()
        {
            SubTest(1, 5, 1, 4, 1);
            SubTest(ItemConst.EmptyItemId, 5, 1, 0, ItemConst.EmptyItemId);
            SubTest(1, 5, 10, 0, ItemConst.EmptyItemId);
            SubTest(1, 8, 8, 0, ItemConst.EmptyItemId);
            SubTest(1, 8, 9, 0, ItemConst.EmptyItemId);
        }
        public void SubTest(int mid, int mamo, int subamo, int ansamo, int ansID)
        {
            IItemStack mineItemStack;
            if (mid == ItemConst.EmptyItemId)
            {
                mineItemStack = _itemStackFactory.CreatEmpty();
            }
            else
            {
                mineItemStack = _itemStackFactory.Create(mid, mamo);
            }

            var result = mineItemStack.SubItem(subamo);
            Assert.AreEqual(ansamo, result.Count);
            Assert.AreEqual(ansID, result.Id);
        }



        public void ItemAddToOverFlowTest()
        {
            ItemAddToOverFlowTest(2, 299, 0, 0);
            ItemAddToOverFlowTest(2, 299, 1, 0);
            ItemAddToOverFlowTest(2, 150, 150, 0);
            ItemAddToOverFlowTest(2, 300, 1, 1);
            ItemAddToOverFlowTest(2, 1, 300, 1);
            ItemAddToOverFlowTest(2, 300, 300, 300);
        }
        public void ItemAddToOverFlowTest(int id, int baseAmo, int addAmo, int overflowAmo)
        {
            var baseItem = _itemStackFactory.Create(id, baseAmo);


            var result = baseItem.AddItem(_itemStackFactory.Create(id, addAmo));
            Assert.IsTrue(_itemStackFactory.Create(id, overflowAmo).Equals(result.RemainderItemStack));
        }


        public void ItemAddToOverFlowThrowTest()
        {
            ItemAddToOverFlowThrowTest(1, 50, false);
            ItemAddToOverFlowThrowTest(1, 51, true);
            ItemAddToOverFlowThrowTest(1, 100, true);
        }
        public void ItemAddToOverFlowThrowTest(int id, int baseAmo, bool isthrow)
        {
            try
            {
                _itemStackFactory.Create(id, baseAmo);
                Assert.IsFalse(isthrow);
            }
            catch (Exception e)
            {
                Assert.IsTrue(isthrow);
            }
        }


        //関係ないオブジェクトを渡すFalseになるテスト

        public void NotRelatedObjectPassFalseHaveTest()
        {
            NotRelatedObjectPassFalseHaveTest(0);
            NotRelatedObjectPassFalseHaveTest(1.5);
            NotRelatedObjectPassFalseHaveTest("aaa");
        }
        public void NotRelatedObjectPassFalseHaveTest(object obj)
        {
            IItemStack nullItem = _itemStackFactory.CreatEmpty();
            Assert.IsFalse(nullItem.Equals(obj));
            IItemStack item = _itemStackFactory.Create(5, 1);
            Assert.IsFalse(item.Equals(obj));
        }

        [TestMethod]
        public void ToStringTest()
        {
            IItemStack item = _itemStackFactory.CreatEmpty();
            Assert.IsTrue(item.ToString() == "ID:0 Count:0");
            item = _itemStackFactory.Create(1, 5);
            Assert.IsTrue(item.ToString() == "ID:1 Count:5");
            item = _itemStackFactory.Create(13, 10);
            Assert.IsTrue(item.ToString() == "ID:13 Count:10");
        }
    }
}