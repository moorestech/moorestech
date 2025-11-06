using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChestSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            
            var chestPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var chestBlock = blockFactory.Create(ForUnitTestModBlockId.ChestId, new BlockInstanceId(1), chestPosInfo);
            var chest = chestBlock.GetComponent<VanillaChestComponent>();

            // テスト用にアイテムを設定する際はイベントを発火させない（ブロックがまだWorldBlockDatastoreに登録されていないため）
            // Set items for testing without firing events (block not yet registered in WorldBlockDatastore)
            var chestInventory = (global::Core.Inventory.OpenableInventoryItemDataStoreService)typeof(global::Game.Block.Blocks.Chest.VanillaChestComponent)
                .GetField("_itemDataStoreService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(chest);
            chestInventory.SetItemWithoutEvent(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 7));
            chestInventory.SetItemWithoutEvent(2, ServerContext.ItemStackFactory.Create(new ItemId(2), 45));
            chestInventory.SetItemWithoutEvent(4, ServerContext.ItemStackFactory.Create(new ItemId(3), 3));
            
            var save = chest.GetSaveState();
            var states = new Dictionary<string, string>() { { chest.SaveKey, save } };
            Debug.Log(save);
            
            var chestBlock2 = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, chestPosInfo);
            var chest2 = chestBlock2.GetComponent<VanillaChestComponent>();
            
            Assert.AreEqual(chest.GetItem(0), chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2), chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(4), chest2.GetItem(4));
        }
    }
}