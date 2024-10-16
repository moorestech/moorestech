using System.Reflection;
using Core.Inventory;
using Core.Master;
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
    /// <summary>
    /// This test class verifies the save and load functionality of the GearMiner block.
    /// It ensures that the miner's state, including its inventory and remaining mining time, is correctly preserved.
    /// </summary>
    public class GearMinerSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            // Initialize the server and get the block factory.
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = ServerContext.BlockFactory;

            // Get the block GUID for the GearMiner.
            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMiner).BlockGuid;

            // Create the position info for the miner.
            var minerPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);

            // Create an instance of the GearMiner block.
            var originalMiner = blockFactory.Create(ForUnitTestModBlockId.GearMiner, new BlockInstanceId(1), minerPosInfo);
            var originalMinerComponent = originalMiner.GetComponent<VanillaMinerProcessorComponent>();

            // Set the remaining mining time to a specific value.
            var originalRemainingSecond = 0.35;

            // Access the miner's inventory using reflection to set test items.
            var inventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerProcessorComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMinerComponent);

            // Set some items in the miner's inventory.
            inventory.SetItem(0, new ItemId(1), 1);
            inventory.SetItem(2, new ItemId(4), 1);

            // Set the remaining mining time using reflection.
            typeof(VanillaMinerProcessorComponent)
                .GetField("_remainingSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(originalMinerComponent, originalRemainingSecond);

            // Save the state of the miner to a JSON string.
            var json = originalMiner.GetSaveState();
            Debug.Log(json);

            // Load a new miner instance from the saved state.
            var loadedMiner = blockFactory.Load(minerGuid, new BlockInstanceId(2), json, minerPosInfo);
            var loadedMinerComponent = loadedMiner.GetComponent<VanillaMinerProcessorComponent>();

            // Access the loaded miner's inventory and remaining mining time.
            var loadedInventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerProcessorComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedMinerComponent);

            var loadedRemainingSecond =
                (double)typeof(VanillaMinerProcessorComponent)
                    .GetField("_remainingSecond", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedMinerComponent);

            // Assert that the original and loaded inventories are equal.
            Assert.AreEqual(inventory.GetItem(0), loadedInventory.GetItem(0));
            Assert.AreEqual(inventory.GetItem(1), loadedInventory.GetItem(1));
            Assert.AreEqual(inventory.GetItem(2), loadedInventory.GetItem(2));

            // Assert that the remaining mining time is the same.
            Assert.AreEqual(originalRemainingSecond, loadedRemainingSecond);
        }
    }
}
