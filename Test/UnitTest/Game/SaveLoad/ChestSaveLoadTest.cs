using Core.Block.BlockFactory;
using Core.Block.Blocks.Chest;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module.TestConfig;

namespace Test.UnitTest.Game.SaveLoad
{
    public class ChestSaveLoadTest
    {
        private const int ChestBlockId = 7;

        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);

            var blockFactory = serviceProvider.GetService<BlockFactory>();

            var chest = (VanillaChest) blockFactory.Create(ChestBlockId,1);
            
            
            chest.SetItem(0,5,7);
            chest.SetItem(2,10,45);
            chest.SetItem(6,100,3);

            var save = chest.GetSaveState();
            
            var chest2 = (VanillaChest) blockFactory.Load(ChestBlockId,1,save);
            
            Assert.AreEqual(chest.GetItem(0),chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2),chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(6),chest2.GetItem(6));
        }
    }
}