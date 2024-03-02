using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChestSaveLoadTest
    {
        private const int ChestBlockId = 7;

        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var blockHash = serviceProvider.GetService<IBlockConfig>().GetBlockConfig(ChestBlockId).BlockHash;

            var chest = (VanillaChest)blockFactory.Create(ChestBlockId, 1);


            chest.SetItem(0, 1, 7);
            chest.SetItem(2, 2, 45);
            chest.SetItem(4, 3, 3);

            var save = chest.GetSaveState();
            Debug.Log(save);

            var chest2 = (VanillaChest)blockFactory.Load(blockHash, 1, save);

            Assert.AreEqual(chest.GetItem(0), chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2), chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(4), chest2.GetItem(4));
        }
    }
}