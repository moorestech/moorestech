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
    public class MinerSaveLoadTest
    {

        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockFactory = ServerContext.BlockFactory;
            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId).BlockGuid;

            var minerPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var originalMiner = blockFactory.Create(ForUnitTestModBlockId.ElectricMinerId, new BlockInstanceId(1), minerPosInfo);
            var originalMinerComponent = originalMiner.GetComponent<VanillaMinerProcessorComponent>();

            // テスト用のtick数（7 tick）
            // Test remaining ticks (7 ticks)
            var originalRemainingTicks = 7u;

            var inventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerProcessorComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMinerComponent);

            // テスト用にアイテムを設定する際はイベントを発火させない（ブロックがまだWorldBlockDatastoreに登録されていないため）
            // Set items for testing without firing events (block not yet registered in WorldBlockDatastore)
            inventory.SetItemWithoutEvent(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 1));
            inventory.SetItemWithoutEvent(2, ServerContext.ItemStackFactory.Create(new ItemId(4), 1));
            typeof(VanillaMinerProcessorComponent).GetField("_remainingTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(originalMinerComponent, originalRemainingTicks);


            var json = originalMiner.GetSaveState();
            Debug.Log(json);


            var loadedMiner = blockFactory.Load(minerGuid, new BlockInstanceId(1), json, minerPosInfo);
            var loadedMinerComponent = loadedMiner.GetComponent<VanillaMinerProcessorComponent>();
            var loadedInventory =
                (OpenableInventoryItemDataStoreService)typeof(VanillaMinerProcessorComponent)
                    .GetField("_openableInventoryItemDataStoreService", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalMinerComponent);
            var loadedRemainingTicks =
                (uint)typeof(VanillaMinerProcessorComponent)
                    .GetField("_remainingTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedMinerComponent);

            Assert.AreEqual(inventory.GetItem(0), loadedInventory.GetItem(0));
            Assert.AreEqual(inventory.GetItem(1), loadedInventory.GetItem(1));
            Assert.AreEqual(inventory.GetItem(2), loadedInventory.GetItem(2));
            Assert.AreEqual(originalRemainingTicks, loadedRemainingTicks);
        }
    }
}