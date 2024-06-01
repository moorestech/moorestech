using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Context;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var blockFactory = ServerContext.BlockFactory;
            var blockHash = ServerContext.BlockConfig.GetBlockConfig(ChestBlockId).BlockHash;

            var chestPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var chestBlock = blockFactory.Create(ChestBlockId, 1, chestPosInfo);
            var chest = chestBlock.ComponentManager.GetComponent<VanillaChestComponent>();


            chest.SetItem(0, 1, 7);
            chest.SetItem(2, 2, 45);
            chest.SetItem(4, 3, 3);

            var save = chest.GetSaveState();
            Debug.Log(save);

            var chestBlock2 = blockFactory.Load(blockHash, 1, save, chestPosInfo);
            var chest2 = chestBlock2.ComponentManager.GetComponent<VanillaChestComponent>();

            Assert.AreEqual(chest.GetItem(0), chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2), chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(4), chest2.GetItem(4));
        }
    }
}