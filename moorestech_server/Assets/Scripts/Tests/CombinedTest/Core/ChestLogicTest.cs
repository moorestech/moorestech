using System.Collections.Generic;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Component.IOConnector;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Core
{
    public class ChestLogicTest
    {
        //ベルトコンベアからアイテムを搬入する
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;

            var random = new Random(4123);

            var id = random.Next(1, 11);
            var count = 1;
            var item = itemStackFactory.Create(id, count);
            
            var chest = blockFactory.Create(7, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var chestComponent = chest.ComponentManager.GetComponent<VanillaChestComponent>();

            var beltConveyor = blockFactory.Create(3, int.MaxValue, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.ComponentManager.GetComponent<VanillaBeltConveyorComponent>();
            beltConveyorComponent.InsertItem(item);

            var beltConnectInventory = (List<IBlockInventory>)beltConveyor.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;
            beltConnectInventory.Add(chestComponent);


            while (!chestComponent.GetItem(0).Equals(item)) GameUpdater.UpdateWithWait();

            Assert.True(chestComponent.GetItem(0).Equals(item));
        }

        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var blockFactory = ServerContext.BlockFactory;

            var chest = blockFactory.Create(7, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var chestComponent = chest.ComponentManager.GetComponent<VanillaChestComponent>();
            
            var beltconveyor = blockFactory.Create(3, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltconveyor.ComponentManager.GetComponent<VanillaBeltConveyorComponent>();

            chestComponent.SetItem(0, 1, 1);

            var chestConnectInventory = (List<IBlockInventory>)chest.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;
            chestConnectInventory.Add(beltConveyorComponent);
            GameUpdater.UpdateWithWait();


            Assert.AreEqual(chestComponent.GetItem(0).Count, 0);
        }
    }
}