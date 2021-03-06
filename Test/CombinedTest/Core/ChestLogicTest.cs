using System;
using System.Threading;
using Core.Block.BlockFactory;
using Core.Block.Blocks.BeltConveyor;
using Core.Block.Blocks.Chest;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;
using Core.Update;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;

using Test.Module;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Core
{
    public class ChestLogicTest
    {


        //ベルトコンベアからアイテムを搬入する
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            var (_, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);


            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var config = (BeltConveyorConfigParam) blockConfig.GetBlockConfig(3).Param;
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            var random = new Random(4123);

            int id = random.Next(1, 11);
            int count = 1;
            var item = itemStackFactory.Create(id, count);
            var chest = (VanillaChest) blockFactory.Create(7, 0);
            var beltConveyor = (VanillaBeltConveyor) blockFactory.Create(3, Int32.MaxValue);
            beltConveyor.AddOutputConnector(chest);
            
            var expectedEndTime = DateTime.Now.AddMilliseconds(
                config.TimeOfItemEnterToExit);
            var outputItem = beltConveyor.InsertItem(item);
            while (!chest.GetItem(0).Equals(item))
            {
                GameUpdate.Update();
            }

            Assert.True(chest.GetItem(0).Equals(item));

        }
        
        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            var chest = (VanillaChest)blockFactory.Create(7, 0);
            var beltconveyor = (VanillaBeltConveyor)blockFactory.Create(3, 0);
            
            
            chest.SetItem(0,1,1);
            
            chest.AddOutputConnector(beltconveyor);
            GameUpdate.Update();
            
            
            Assert.AreEqual(chest.GetItem(0).Count,0);
            

        }
    }
}