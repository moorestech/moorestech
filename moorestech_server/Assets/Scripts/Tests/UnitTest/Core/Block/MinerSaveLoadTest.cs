using System.Reflection;
using Core.Inventory;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Core.Block
{
    public class MinerSaveLoadTest
    {
        private const int MinerId = UnitTestModBlockId.MinerId;

        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var minerHash = serviceProvider.GetService<IBlockConfig>().GetBlockConfig(MinerId).BlockHash;

            var originalMiner = blockFactory.Create(MinerId, 1);
            var originalRemainingMillSecond = 350;

            var inventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerBase)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMiner);
            inventory.SetItem(0, 1, 1);
            inventory.SetItem(2, 4, 1);
            typeof(VanillaMinerBase).GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(originalMiner, originalRemainingMillSecond);


            var json = originalMiner.GetSaveState();
            Debug.Log(json);


            var loadedMiner = blockFactory.Load(minerHash, 1, json);
            var loadedInventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerBase)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMiner);
            var loadedRemainingMillSecond =
                (int)typeof(VanillaMinerBase)
                    .GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedMiner);

            Assert.AreEqual(inventory.GetItem(0), loadedInventory.GetItem(0));
            Assert.AreEqual(inventory.GetItem(1), loadedInventory.GetItem(1));
            Assert.AreEqual(inventory.GetItem(2), loadedInventory.GetItem(2));
            Assert.AreEqual(originalRemainingMillSecond, loadedRemainingMillSecond);
        }
    }
}