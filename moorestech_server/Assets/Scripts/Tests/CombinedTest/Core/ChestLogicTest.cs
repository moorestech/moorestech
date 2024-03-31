using System.Collections.Generic;
using Server.Core.Item;
using Server.Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
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
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var random = new Random(4123);

            var id = random.Next(1, 11);
            var count = 1;
            var item = itemStackFactory.Create(id, count);
            var chest = (VanillaChest)blockFactory.Create(7, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));

            var beltConveyor = (VanillaBeltConveyor)blockFactory.Create(3, int.MaxValue, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            beltConveyor.InsertItem(item);

            var beltConnectInventory = (List<IBlockInventory>)beltConveyor.ComponentManager.GetComponent<InputConnectorComponent>().ConnectInventory;
            beltConnectInventory.Add(chest);


            while (!chest.GetItem(0).Equals(item)) GameUpdater.UpdateWithWait();

            Assert.True(chest.GetItem(0).Equals(item));
        }

        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var chest = (VanillaChest)blockFactory.Create(7, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltconveyor = (VanillaBeltConveyor)blockFactory.Create(3, 0, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));

            chest.SetItem(0, 1, 1);

            var chestConnectInventory = (List<IBlockInventory>)chest.ComponentManager.GetComponent<InputConnectorComponent>().ConnectInventory;
            chestConnectInventory.Add(beltconveyor);
            GameUpdater.UpdateWithWait();


            Assert.AreEqual(chest.GetItem(0).Count, 0);
        }
    }
}