using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// Test class for the gear-powered miner component.
    /// </summary>
    public class GearMinerMiningTest
    {
        /// <summary>
        /// Tests that the gear miner produces items after the required mining time when supplied with correct RPM and torque.
        /// </summary>
        [Test]
        public void GearMiningTest()
        {
            // Initialize the dependency injection container and services.
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // Retrieve necessary services.
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            
            // Locate a map vein (resource deposit) to mine.
            var (mapVein, position) = MinerMiningTest.GetMapVein();
            Assert.NotNull(mapVein, "No map vein found for mining.");

            // Add the gear miner block at the vein position.
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMiner, position, BlockDirection.North, out var gearMinerBlock);
            var gearMiner = worldBlockDatastore.GetBlock(position);

            // Retrieve the mining processor component from the gear miner.
            var minerProcessorComponent = gearMiner.GetComponent<VanillaMinerProcessorComponent>();

            // Use reflection to access private fields: _miningItems and _defaultMiningTime.
            var miningItemsField = typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var miningTimeField = typeof(VanillaMinerProcessorComponent).GetField("_defaultMiningTime", BindingFlags.NonPublic | BindingFlags.Instance);
            var miningItems = (List<IItemStack>)miningItemsField.GetValue(minerProcessorComponent);
            var miningItemId = miningItems[0].Id;
            var miningTime = (float)miningTimeField.GetValue(minerProcessorComponent);

            // Create a dummy inventory to receive mined items.
            var dummyInventory = new DummyBlockInventory();

            // Connect the dummy inventory to the miner's output.
            var minerConnectors = (Dictionary<IBlockInventory, ConnectedInfo>)gearMiner
                .GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            minerConnectors.Add(dummyInventory, new ConnectedInfo());

            // Place a gear generator adjacent to the gear miner to supply RPM and torque.
            var generatorPosition = position + new Vector3Int(0, 0, -1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, out var generatorBlock);

            // Ensure the gear network is updated so that the miner receives power.
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // Wait for the mining time to elapse.
            var mineEndTime = DateTime.Now.AddSeconds(miningTime * 1.2f);
            while (DateTime.Now < mineEndTime)
            {
                GameUpdater.UpdateWithWait();
            }

            // Verify that one item has been mined and transferred to the dummy inventory.
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id, "The mined item ID does not match.");
            Assert.AreEqual(1, dummyInventory.InsertedItems[0].Count, "The mined item count should be 1.");

            // Disconnect the dummy inventory to test internal storage.
            minerConnectors.Remove(dummyInventory);

            // Wait for two more mining cycles.
            mineEndTime = DateTime.Now.AddSeconds(miningTime * 2.2f);
            while (DateTime.Now < mineEndTime)
            {
                GameUpdater.UpdateWithWait();
            }

            // Check that two items are stored in the miner's internal inventory.
            var outputSlot = minerProcessorComponent.InventoryItems[0];
            Assert.AreEqual(miningItemId, outputSlot.Id, "The stored item ID does not match.");
            Assert.AreEqual(2, outputSlot.Count, "The stored item count should be 2.");

            // Reconnect the dummy inventory.
            minerConnectors.Add(dummyInventory, new ConnectedInfo());

            // Update the game state to allow the miner to transfer items to the dummy inventory.
            GameUpdater.UpdateWithWait();

            // Verify that a total of three items are now in the dummy inventory.
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id, "The mined item ID does not match after reconnection.");
            Assert.AreEqual(3, dummyInventory.InsertedItems[0].Count, "The total mined item count should be 3 after reconnection.");
        }
    }
}
