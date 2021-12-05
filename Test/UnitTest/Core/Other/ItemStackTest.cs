using System;
using Core.Item;
using Core.Item.Implementation;
using Core.Item.Util;
using NUnit.Framework;

namespace Test.UnitTest.Core.Other
{
    public class ItemStackTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(1,1,1,1,2,0,1,ItemConst.NullItemId)]
        [TestCase(1,5,1,1,6,0,1,ItemConst.NullItemId)]
        [TestCase(ItemConst.NullItemId,0,1,3,3,0,1,ItemConst.NullItemId)]
        [TestCase(ItemConst.NullItemId,0,2,9,9,0,2,ItemConst.NullItemId)]
        [TestCase(ItemConst.NullItemId,5,1,1,1,0,1,ItemConst.NullItemId)]
        [TestCase(1,1,ItemConst.NullItemId,0,1,0,1,ItemConst.NullItemId)]
        [TestCase(1,1,ItemConst.NullItemId,0,1,0,1,ItemConst.NullItemId)]
        [TestCase(1,5,ItemConst.NullItemId,0,5,0,1,ItemConst.NullItemId)]
        [TestCase(3,1,1,8,1,8,3,1)]
        [TestCase(1,1,3,1,1,1,1,3)]
        [TestCase(2,5,5,3,5,3,2,5)]
        public void AddTest(int mid,int mamo,int rid,int ramo,int ansMAmo,int ansRAmo,int ansMid,int ansRID)
        {
            IItemStack mineItemStack;
            if (mid == ItemConst.NullItemId)
            {
                mineItemStack = ItemStackFactory.CreatEmpty();
            }
            else
            {
                mineItemStack = ItemStackFactory.Create(mid,mamo);
            }
            IItemStack receivedItemStack;
            if (rid == ItemConst.NullItemId)
            {
                receivedItemStack = ItemStackFactory.CreatEmpty();
            }
            else
            {
                receivedItemStack = ItemStackFactory.Create(rid,ramo);
            }
            var result = mineItemStack.AddItem(receivedItemStack);
            Assert.AreEqual(result.ProcessResultItemStack.Amount, ansMAmo);
            Assert.AreEqual(result.RemainderItemStack.Amount, ansRAmo);
            Assert.AreEqual(result.ProcessResultItemStack.Id, ansMid);
            Assert.AreEqual(result.RemainderItemStack.Id, ansRID);
        }

        [TestCase(1,5,1,4,1)]
        [TestCase(ItemConst.NullItemId,5,1,0,ItemConst.NullItemId)]
        [TestCase(1,5,10,0,ItemConst.NullItemId)]
        [TestCase(1,8,8,0,ItemConst.NullItemId)]
        [TestCase(1,8,9,0,ItemConst.NullItemId)]
        public void SubTest(int mid, int mamo, int subamo, int ansamo, int ansID)
        {
            IItemStack mineItemStack;
            if (mid == ItemConst.NullItemId)
            {
                mineItemStack = ItemStackFactory.CreatEmpty();
            }
            else
            {
                mineItemStack = ItemStackFactory.Create(mid,mamo);
            }

            var result = mineItemStack.SubItem(subamo);            
            Assert.AreEqual(ansamo,result.Amount);
            Assert.AreEqual(ansID,result.Id);

        }

        

        [TestCase(2,299,0,0)]
        [TestCase(2,299,1,0)]
        [TestCase(2,150,150,0)]
        [TestCase(2,300,1,1)]
        [TestCase(2,1,300,1)]
        [TestCase(2,300,300,300)]
        public void ItemAddToOverFlowTest(int id,int baseAmo,int addAmo,int overflowAmo)
        {
            var baseItem = ItemStackFactory.Create(id, baseAmo);
            
            
            var result = baseItem.AddItem(ItemStackFactory.Create(id, addAmo));
            Assert.True(ItemStackFactory.Create(id,overflowAmo).Equals(result.RemainderItemStack));
            
        }
        
        [TestCase(1,50,false)]
        [TestCase(1,51,true)]
        [TestCase(1,100,true)]
        public void ItemAddToOverFlowThrowTest(int id,int baseAmo,bool isthrow)
        {
            try
            {
                ItemStackFactory.Create(id, baseAmo);
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
            IItemStack nullItem = ItemStackFactory.CreatEmpty();
            Assert.False(nullItem.Equals(obj));
            IItemStack item = ItemStackFactory.Create(5,1);
            Assert.False(item.Equals(obj));
        }

        [Test]
        public void ToStringTest()
        {
            IItemStack item = ItemStackFactory.CreatEmpty();
            Assert.True(item.ToString() == "ID:0 Amount:0");
            item = ItemStackFactory.Create(1, 5);
            Assert.True(item.ToString() == "ID:1 Amount:5");
            item = ItemStackFactory.Create(13, 10);
            Assert.True(item.ToString() == "ID:13 Amount:10");
        }
    }
}