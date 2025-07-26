using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BaseCamp;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class BaseCampTest
    {
        [Test]
        public void SimpleSingleItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            // ベースキャンプコンポーネントの取得
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 必要なアイテムの設定（テスト用に単一アイテム）
            var requiredItemGuid = new Guid("00000000-0000-0000-1234-000000000001");
            var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItemGuid);
            var requiredAmount = 10; // BaseCamp1は10個必要
            
            // アイテムを納品
            var deliveredItem = itemStackFactory.Create(requiredItemId, requiredAmount);
            baseCampInventory.InsertItem(deliveredItem);
            
            // 納品完了の確認
            Assert.IsTrue(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void MultipleItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp2;
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(2), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 複数の必要アイテム（実際のBaseCamp2の設定に合わせる）
            var requiredItems = new List<(ItemId id, int count)>
            {
                (new ItemId(1), 3),
                (new ItemId(2), 2),
                (new ItemId(3), 5)
            };
            
            // 部分的に納品
            var remaining1 = baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[0].id, requiredItems[0].count));
            Debug.Log($"After first insert: remaining={remaining1.Count}");
            Assert.IsFalse(baseCampComponent.IsCompleted());
            
            var remaining2 = baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[1].id, requiredItems[1].count));
            Debug.Log($"After second insert: remaining={remaining2.Count}");
            Assert.IsFalse(baseCampComponent.IsCompleted());
            
            // 最後のアイテムを納品
            var remaining3 = baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[2].id, requiredItems[2].count));
            Debug.Log($"After third insert: remaining={remaining3.Count}");
            Debug.Log($"IsCompleted: {baseCampComponent.IsCompleted()}, Progress: {baseCampComponent.GetProgress()}");
            
            // インベントリの状態を確認
            for (int i = 0; i < baseCampComponent.GetSlotSize(); i++)
            {
                var item = baseCampComponent.GetItem(i);
                Debug.Log($"Slot {i}: {item.Id} x {item.Count}");
            }
            
            Assert.IsTrue(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void WrongItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(3), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 間違ったアイテムを納品しようとする（BaseCamp1はItemId 1が必要だが、ItemId 2を送る）
            var wrongItemGuid = new Guid("00000000-0000-0000-1234-000000000002");
            var wrongItemId = MasterHolder.ItemMaster.GetItemId(wrongItemGuid);
            var wrongItem = itemStackFactory.Create(wrongItemId, 10);
            
            var remaining = baseCampInventory.InsertItem(wrongItem);
            
            // 間違ったアイテムは受け付けないことを確認（全て返される）
            Assert.AreEqual(10, remaining.Count);
            Assert.IsFalse(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void PartialDeliveryProgressTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(4), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            var requiredItemId = new ItemId(1);
            var requiredAmount = 10;
            
            // 部分的に納品
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 3));
            Assert.AreEqual(0.3f, baseCampComponent.GetProgress(), 0.01f);
            
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 4));
            Assert.AreEqual(0.7f, baseCampComponent.GetProgress(), 0.01f);
            
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 3));
            Assert.AreEqual(1.0f, baseCampComponent.GetProgress(), 0.01f);
            Assert.IsTrue(baseCampComponent.IsCompleted());
        }
    }
    
}