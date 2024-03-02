using System;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

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
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var random = new Random(4123);

            var id = random.Next(1, 11);
            var count = 1;
            var item = itemStackFactory.Create(id, count);
            var chest = (VanillaChest)blockFactory.Create(7, 0);
            var beltConveyor = (VanillaBeltConveyor)blockFactory.Create(3, int.MaxValue);
            beltConveyor.AddOutputConnector(chest);

            var expectedEndTime = DateTime.Now.AddMilliseconds(
                config.TimeOfItemEnterToExit);
            var outputItem = beltConveyor.InsertItem(item);
            while (!chest.GetItem(0).Equals(item)) GameUpdater.UpdateWithWait();

            Assert.True(chest.GetItem(0).Equals(item));
        }

        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();


            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var chest = (VanillaChest)blockFactory.Create(7, 0);
            var beltconveyor = (VanillaBeltConveyor)blockFactory.Create(3, 0);


            chest.SetItem(0, 1, 1);

            chest.AddOutputConnector(beltconveyor);
            GameUpdater.UpdateWithWait();


            Assert.AreEqual(chest.GetItem(0).Count, 0);
        }
    }
}