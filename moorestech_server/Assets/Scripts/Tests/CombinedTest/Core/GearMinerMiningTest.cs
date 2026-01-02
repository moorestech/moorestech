using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Chest;
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
    public class GearMinerMiningTest
    {
        /// <summary>
        /// 正しい RPM とトルクが供給された場合、ギア マイナーが必要な採掘時間後にアイテムを生成するかどうかをテスト
        /// Tests that the gear miner produces items after the required mining time when supplied with correct RPM and torque.
        /// </summary>
        [Test]
        public void GearMiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            
            // 鉱石を採掘するための鉱脈を探す
            // Locate a map vein (resource deposit) to mine.
            var (mapVein, position) = MinerMiningTest.GetMapVein();
            Assert.NotNull(mapVein, "No map vein found for mining.");

            // 鉱脈の位置に歯車採掘機を追加する
            // Add the gear miner block at the vein position.
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMiner, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearMinerBlock);
            var gearMiner = worldBlockDatastore.GetBlock(position);

            // リフレクションを使用してプライベート フィールドにアクセスする: _miningItems と _defaultMiningTime
            // Use reflection to access private fields: _miningItems and _defaultMiningTime.
            var minerProcessorComponent = gearMiner.GetComponent<VanillaMinerProcessorComponent>();
            var miningItemsField = typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var miningTimeField = typeof(VanillaMinerProcessorComponent).GetField("_defaultMiningTime", BindingFlags.NonPublic | BindingFlags.Instance);
            var miningItems = (List<IItemStack>)miningItemsField.GetValue(minerProcessorComponent);
            var miningItemId = miningItems[0].Id;
            var miningTime = (float)miningTimeField.GetValue(minerProcessorComponent);
            
            
            // 採掘機に RPM とトルクを供給するために、歯車ジェネレータを採掘機の隣に配置する
            // Place a gear generator adjacent to the gear miner to supply RPM and torque.
            var generatorPosition = position + new Vector3Int(0, 0, -1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            
            // 歯車エネルギーを受け取るために歯車ネットワークを更新
            // Ensure the gear network is updated so that the miner receives power.
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();
            
            // アイテムがアウトプットされていることを確認するためのチェストを採掘機の隣に設置する
            // Place a chest adjacent to the gear miner to verify that items are output.
            var chestBlockPos = position + new Vector3Int(1, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestBlockPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chestBlock);
            var chestComponent = chestBlock.GetComponent<VanillaChestComponent>();
            
            // 採掘中待機する
            // Wait for the mining time to elapse.
            var mineEndTime = DateTime.Now.AddSeconds(miningTime * 1.2f);
            while (DateTime.Now < mineEndTime)
            {
                GameUpdater.UpdateWithWait();
            }

            // アイテムが中にはいっていることを確認
            // Check that an item is stored inside.
            Assert.AreEqual(miningItemId, chestComponent.InventoryItems[0].Id, "The mined item ID does not match.");
            Assert.AreEqual(1, chestComponent.InventoryItems[0].Count, "The mined item count should be 1.");

            // チェストを破壊して、採掘機の中にアイテムが残ることをチェックする
            // Destroy the chest and check that the item remains inside the miner.
            worldBlockDatastore.RemoveBlock(chestBlockPos, BlockRemoveReason.ManualRemove);

            // 2回分の採掘時間待機
            // Wait for two more mining cycles.
            mineEndTime = DateTime.Now.AddSeconds(miningTime * 2.2f);
            while (DateTime.Now < mineEndTime)
            {
                GameUpdater.UpdateWithWait();
            }

            // 採掘機の中にアイテムが残っていることを確認
            // Check that two items are stored inside the miner.
            var outputSlot = minerProcessorComponent.InventoryItems[0];
            Assert.AreEqual(miningItemId, outputSlot.Id, "The stored item ID does not match.");
            Assert.AreEqual(2, outputSlot.Count, "The stored item count should be 2.");

            // 再びチェストを設置する
            // Place the chest again.
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestBlockPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out chestBlock);
            chestComponent = chestBlock.GetComponent<VanillaChestComponent>();

            // チェストにアイテムが入っていることを確認するためにアップデート
            // Update to ensure that the chest contains the items.
            GameUpdater.UpdateWithWait();

            // アイテムがさらに 2 個入っていることを確認
            // Check that two more items are stored inside the chest.
            Assert.AreEqual(miningItemId, chestComponent.InventoryItems[0].Id, "The mined item ID does not match after reconnection.");
            Assert.AreEqual(2, chestComponent.InventoryItems[0].Count, "The total mined item count should be 3 after reconnection.");
        }
    }
}
