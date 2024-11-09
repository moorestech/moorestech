using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
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
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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
            
            var service = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            
            service.InsertItem(itemStackFactory.Create(new ItemId(1), 4));
            service.InsertItem(itemStackFactory.Create(new ItemId(2), 3));
            service.InsertItem(itemStackFactory.Create(new ItemId(3), 2));
            service.InsertItem(itemStackFactory.Create(new ItemId(4), 1));
            
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 4), inventory1.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 3), inventory2.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 2), inventory3.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 1), inventory2.InsertedItems[1]);
        }
    }
}