#if NET6_0
using System;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class BeltConveyorInsertTest
    {
        //2
        [Test]
        public void TwoItemIoTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            var inputChest = (VanillaChest)blockFactory.Create(UnitTestModBlockId.ChestId, 1);
            var beltConveyor = blockFactory.Create(UnitTestModBlockId.BeltConveyorId, 2);
            var outputChest = (VanillaChest)blockFactory.Create(UnitTestModBlockId.ChestId, 3);

            
            worldBlockDatastore.AddBlock(inputChest, 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(beltConveyor, 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(outputChest, 0, 2, BlockDirection.North);

            //2
            inputChest.SetItem(0, 1, 2);

            //6
            var now = DateTime.Now;
            while (DateTime.Now - now < TimeSpan.FromSeconds(5)) GameUpdater.Update();

            
            Assert.AreEqual(0, inputChest.GetItem(0).Count);
            
            Assert.AreEqual(2, outputChest.GetItem(0).Count);
        }
    }
}
#endif