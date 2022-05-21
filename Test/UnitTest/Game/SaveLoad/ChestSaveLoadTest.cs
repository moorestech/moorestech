using System;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Chest;
using Core.Block.Config;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class ChestSaveLoadTest
    {
        private const int ChestBlockId = 7;

        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var blockHash = serviceProvider.GetService<IBlockConfig>().GetBlockConfig(ChestBlockId).BlockHash;

            var chest = (VanillaChest) blockFactory.Create(ChestBlockId,1);
            
            
            chest.SetItem(0,1,7);
            chest.SetItem(2,2,45);
            chest.SetItem(4,3,3);

            var save = chest.GetSaveState();
            Console.WriteLine(save);
            
            var chest2 = (VanillaChest) blockFactory.Load(blockHash,1,save);
            
            Assert.AreEqual(chest.GetItem(0),chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2),chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(4),chest2.GetItem(4));
        }
    }
}