using System.Collections.Generic;
using Core.Master;
using Core.Item.Interface;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Connector;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Core.Other
{
    public class ConnectingInventoryListPriorityInsertItemServiceTest
    {
        /// <summary>
        ///     アイテムを挿入の優先度がループしてるかテストする
        /// </summary>
        [Test]
        public void Test()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var inventoryList = new List<IBlockInventory>();
            
            //インベントリ1はインベントリのサイズを1にして、インベントリ2に入るか確認する
            var inventory1 = new DummyBlockInventory(1, 1);
            var inventory2 = new DummyBlockInventory();
            var inventory3 = new DummyBlockInventory();
            inventoryList.Add(inventory1);
            inventoryList.Add(inventory2);
            inventoryList.Add(inventory3);
            
            var componentPos = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var inputConnectorComponent = new BlockConnectorComponent<IBlockInventory>(null, null, componentPos);
            
            var targets = (Dictionary<IBlockInventory, ConnectedInfo>)inputConnectorComponent.ConnectedTargets;
            
            foreach (var inventory in inventoryList) targets.Add(inventory, new ConnectedInfo());
            
            var service = new ConnectingInventoryListPriorityInsertItemService(new BlockInstanceId(1), inputConnectorComponent);
            
            service.InsertItem(itemStackFactory.Create(new ItemId(1), 4));
            service.InsertItem(itemStackFactory.Create(new ItemId(2), 3));
            service.InsertItem(itemStackFactory.Create(new ItemId(3), 2));
            service.InsertItem(itemStackFactory.Create(new ItemId(4), 1));
            
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 4), inventory1.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 3), inventory2.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 2), inventory3.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 1), inventory2.InsertedItems[1]);
        }

        [Test]
        public void CanInsertToNextTargetReturnsFalseWhenNextTargetIsFull()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var itemId = new ItemId(1);
            var fullInventory = new ConfigurableBlockInventory(1, 0, true, false);
            var emptyInventory = new ConfigurableBlockInventory(1, 1, true, false);

            // 次の搬出先だけを満杯にし、別搬出先の空きに逃げないことを確認する
            // Fill only the next target and verify the check does not fall through
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(itemId).MaxStack;
            fullInventory.SetItem(0, itemStackFactory.Create(itemId, maxStack));
            var service = CreateService(fullInventory, emptyInventory);

            Assert.IsFalse(service.CanInsertToNextTarget());
        }

        [Test]
        public void CanInsertToNextTargetReturnsTrueWhenNextTargetHasEmptySlot()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var emptyInventory = new ConfigurableBlockInventory(1, 1, true, false);
            var service = CreateService(emptyInventory);

            Assert.IsTrue(service.CanInsertToNextTarget());
        }

        [Test]
        public void ChestUpdateSkipsSourceSlotsWhenNextTargetIsLocked()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inserter = new CountingLockedInserter();
            var item = ServerContext.ItemStackFactory.Create(new ItemId(1), 1);
            var componentStates = CreateChestComponentStates(item);
            var chest = new VanillaChestComponent(componentStates, new BlockInstanceId(10), 1, inserter);

            // 搬出先ロック時は中身があってもInsertItemまで到達しない
            // A locked destination prevents InsertItem even when the source has items
            chest.Update();

            Assert.AreEqual(0, inserter.GetInsertCallCount());
            Assert.AreEqual(item, chest.GetItem(0));
        }

        private static ConnectingInventoryListPriorityInsertItemService CreateService(params IBlockInventory[] inventories)
        {
            var componentPos = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var inputConnectorComponent = new BlockConnectorComponent<IBlockInventory>(null, null, componentPos);
            var targets = (Dictionary<IBlockInventory, ConnectedInfo>)inputConnectorComponent.ConnectedTargets;

            // テストでは登録順をそのまま次の搬出先として使う
            // Use registration order as the next output target in tests
            foreach (var inventory in inventories) targets.Add(inventory, new ConnectedInfo());

            return new ConnectingInventoryListPriorityInsertItemService(new BlockInstanceId(1), inputConnectorComponent);
        }

        private static Dictionary<string, string> CreateChestComponentStates(IItemStack itemStack)
        {
            var saveObjects = new List<ItemStackSaveJsonObject> { new(itemStack) };
            return new Dictionary<string, string>
            {
                [typeof(VanillaChestComponent).FullName] = JsonConvert.SerializeObject(saveObjects),
            };
        }

        private sealed class CountingLockedInserter : IBlockInventoryInserter, IBlockInventoryInsertTargetState
        {
            private int _insertCallCount;

            public IItemStack InsertItem(IItemStack itemStack)
            {
                _insertCallCount++;
                return itemStack.SubItem(itemStack.Count);
            }

            public bool CanInsertToNextTarget()
            {
                return false;
            }

            public int GetInsertCallCount()
            {
                return _insertCallCount;
            }
        }
    }
}
