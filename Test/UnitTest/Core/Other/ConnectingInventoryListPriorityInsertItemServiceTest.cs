using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Service;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;

using Test.Module;

using Test.Module.TestMod;

namespace Test.UnitTest.Core.Other
{
    public class ConnectingInventoryListPriorityInsertItemServiceTest
    {
        /// <summary>
        /// アイテムを挿入の優先度がループしてるかテストする
        /// </summary>
        [Test]
        public void Test()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var inventoryList = new List<IBlockInventory>();
            
            //インベントリ1はインベントリのサイズを1にして、インベントリ2に入るか確認する
            var inventory1 = new DummyBlockInventory(itemStackFactory,1,1);
            var inventory2 = new DummyBlockInventory(itemStackFactory);
            var inventory3 = new DummyBlockInventory(itemStackFactory);
            inventoryList.Add(inventory1);
            inventoryList.Add(inventory2);
            inventoryList.Add(inventory3);

            var service = new ConnectingInventoryListPriorityInsertItemService(inventoryList);

            service.InsertItem(itemStackFactory.Create(1, 4));
            service.InsertItem(itemStackFactory.Create(2, 3));
            service.InsertItem(itemStackFactory.Create(3, 2));
            service.InsertItem(itemStackFactory.Create(4, 1));

            Assert.AreEqual(itemStackFactory.Create(1, 4),inventory1.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(2, 3),inventory2.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(3, 2),inventory3.InsertedItems[0]);
            Assert.AreEqual(itemStackFactory.Create(4, 1),inventory2.InsertedItems[1]);

        }
    }
}