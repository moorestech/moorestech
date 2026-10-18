using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ItemStackMetaTest
    {
        [Test]
        // メタデータの同一性の評価
        public void MetaDataEqualityTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemsStackFactory = ServerContext.ItemStackFactory;
            
            var meta = new Dictionary<string, ItemStackMetaData> { { "test1", new TestMeta1() } };
            
            var itemStack1 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            var itemStack2 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            
            Assert.IsTrue(itemStack1.Equals(itemStack2));
            
            meta.Add("test2", new TestMeta2());
            
            Assert.IsTrue(itemStack1.Equals(itemStack2));
            
            var itemStack3 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            
            Assert.IsFalse(itemStack1.Equals(itemStack3));
            Assert.IsFalse(itemStack2.Equals(itemStack3));
        }
        
        [Test]
        // Addできる、できないの評価
        public void AddTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemsStackFactory = ServerContext.ItemStackFactory;
            
            var meta = new Dictionary<string, ItemStackMetaData> { { "test1", new TestMeta1() } };
            
            var itemStack1 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            var itemStack2 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            
            var result = itemStack1.AddItem(itemStack2);
            Assert.AreEqual(result.ProcessResultItemStack.Count, 2);
            
            meta.Add("test2", new TestMeta2());
            
            var itemStack3 = itemsStackFactory.Create(new ItemId(1), 1, meta);
            
            var result2 = itemStack1.AddItem(itemStack3);
            Assert.AreEqual(1, result2.ProcessResultItemStack.Count);
            Assert.AreEqual(1, result2.RemainderItemStack.Count);
        }
        
        [TestCase(true)]
        [TestCase(false)]
        public void SuccessfulAdditionPreservesMetadataWithNewInstance(bool emptyDestination)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var factory = ServerContext.ItemStackFactory;
            var itemId = ForUnitTestItemId.ItemId1;
            var metadata = new Dictionary<string, ItemStackMetaData> { { "test1", new TestMeta1() } };

            // 空枠・通常加算とも属性と輸送用IDの契約を保つ
            // Preserve metadata and transport identity contracts for both successful paths
            var incoming = factory.Create(itemId, 2, metadata);
            var destination = emptyDestination ? factory.CreatEmpty() : factory.Create(itemId, 1, metadata);
            var result = destination.AddItem(incoming);
            Assert.AreEqual(emptyDestination ? 2 : 3, result.ProcessResultItemStack.Count);
            Assert.AreEqual(0, result.RemainderItemStack.Count);
            Assert.IsInstanceOf<TestMeta1>(result.ProcessResultItemStack.GetMeta("test1"));
            Assert.AreNotEqual(incoming.ItemInstanceId, result.ProcessResultItemStack.ItemInstanceId);
            Assert.AreNotEqual(destination.ItemInstanceId, result.ProcessResultItemStack.ItemInstanceId);
        }

        [Test]
        // セーブ、ロードの評価
        public void SaveLoadTest()
        {
            //TODO セーブできるようにする
        }
        
        //TODO ベルトコンベアのメタ設定
    }
    
    public class TestMeta1 : ItemStackMetaData
    {
        public override bool Equals(ItemStackMetaData target)
        {
            return target is TestMeta1;
        }
    }
    
    public class TestMeta2 : ItemStackMetaData
    {
        public override bool Equals(ItemStackMetaData target)
        {
            return target is TestMeta2;
        }
    }
}