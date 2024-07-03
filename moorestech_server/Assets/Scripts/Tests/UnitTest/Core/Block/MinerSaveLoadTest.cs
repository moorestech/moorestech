using System.Reflection;
using Core.Inventory;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Core.Block
{
    public class MinerSaveLoadTest
    {
        private const int MinerId = ForUnitTestModBlockId.MinerId;
        
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = ServerContext.BlockFactory;
            var minerHash = ServerContext.BlockConfig.GetBlockConfig(MinerId).BlockHash;
            
            var minerPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var originalMiner = blockFactory.Create(MinerId, new BlockInstanceId(1), minerPosInfo);
            var originalMinerComponent = originalMiner.GetComponent<VanillaElectricMinerComponent>();
            var originalRemainingMillSecond = 350;
            
            var inventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaElectricMinerComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMinerComponent);
            inventory.SetItem(0, 1, 1);
            inventory.SetItem(2, 4, 1);
            typeof(VanillaElectricMinerComponent).GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(originalMinerComponent, originalRemainingMillSecond);
            
            
            var json = originalMiner.GetSaveState();
            Debug.Log(json);
            
            
            var loadedMiner = blockFactory.Load(minerHash, new BlockInstanceId(1), json, minerPosInfo);
            var loadedMinerComponent = loadedMiner.GetComponent<VanillaElectricMinerComponent>();
            var loadedInventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaElectricMinerComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMinerComponent);
            var loadedRemainingMillSecond =
                (int)typeof(VanillaElectricMinerComponent)
                    .GetField("_remainingMillSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedMinerComponent);
            
            Assert.AreEqual(inventory.GetItem(0), loadedInventory.GetItem(0));
            Assert.AreEqual(inventory.GetItem(1), loadedInventory.GetItem(1));
            Assert.AreEqual(inventory.GetItem(2), loadedInventory.GetItem(2));
            Assert.AreEqual(originalRemainingMillSecond, loadedRemainingMillSecond);
        }
    }
}