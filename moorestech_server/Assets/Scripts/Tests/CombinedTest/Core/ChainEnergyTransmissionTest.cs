using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Core.Master;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ChainEnergyTransmissionTest
    {
        [Test]
        public void GeneratorPowerTravelsThroughChain()
        {
            // テスト用DIコンテナを立ち上げる
            // Initialize test DI container
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 0;
            var chainItemId = global::Core.Master.MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            // ブロックを配置してギアネットワークを構築する
            // Place blocks to build gear network
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(6, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(7, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetGear);

            // プレイヤーのインベントリにチェーンを追加する
            // Grant chain item to player inventory
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 5));

            // チェーン接続を確立する
            // Establish chain connection
            var connected = GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(6, 0, 0), playerId, chainItemId, out var error);
            Assert.True(connected);
            Assert.IsEmpty(error ?? string.Empty);

            // ギアネットワークを更新する
            // Update gear networks
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            foreach (var network in gearNetworkDatastore.GearNetworks.Values) network.ManualUpdate();

            // 発電機とターゲットギアの回転が一致することを確認する
            // Ensure generator and target gear share rpm and direction
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            var gear = targetGear.GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }

        [Test]
        public void SaveLoadRestoresChainPoleNetworkConnection()
        {
            // 保存前ワールドにチェーン経由のギアネットワークを構築する
            // Build a gear network through chain poles before saving
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 0;
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(6, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(7, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetGear);

            // チェーン接続を確立して保存対象のインスタンスIDを控える
            // Establish the chain connection and keep instance ids for load assertions
            var inventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 5));
            var connected = GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(6, 0, 0), playerId, chainItemId, out var error);
            Assert.True(connected);
            Assert.IsEmpty(error ?? string.Empty);

            var generatorId = generatorBlock.BlockInstanceId;
            var poleAId = poleA.BlockInstanceId;
            var poleBId = poleB.BlockInstanceId;
            var targetGearId = targetGear.BlockInstanceId;
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(saveServiceProvider);

            // 別DIコンテナへロードして、セーブ復元後のネットワーク所属を検証する
            // Load into another DI container and verify network membership after restore
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            SaveLoadJsonTestHelper.LoadFromJson(loadServiceProvider, saveJson);

            worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var loadedPoleA = worldBlockDatastore.GetBlock(poleAId).GetComponent<IGearChainPole>();
            var loadedPoleB = worldBlockDatastore.GetBlock(poleBId).GetComponent<IGearChainPole>();
            Assert.IsTrue(loadedPoleA.ContainsChainConnection(poleBId));
            Assert.IsTrue(loadedPoleB.ContainsChainConnection(poleAId));

            // チェーン接続先を含めた単一ネットワークへ統合されていることを確認する
            // Ensure the chain-connected endpoints are merged into one network
            Assert.True(GearNetworkDatastore.TryGetGearNetwork(generatorId, out var generatorNetwork));
            Assert.True(GearNetworkDatastore.TryGetGearNetwork(poleAId, out var poleANetwork));
            Assert.True(GearNetworkDatastore.TryGetGearNetwork(poleBId, out var poleBNetwork));
            Assert.True(GearNetworkDatastore.TryGetGearNetwork(targetGearId, out var targetGearNetwork));
            Assert.AreSame(generatorNetwork, poleANetwork);
            Assert.AreSame(generatorNetwork, poleBNetwork);
            Assert.AreSame(generatorNetwork, targetGearNetwork);

            // ロード後の更新で発電機の回転がチェーン先へ届くことを確認する
            // Verify generator rotation reaches the far side after load update
            foreach (var network in loadServiceProvider.GetService<GearNetworkDatastore>().GearNetworks.Values) network.ManualUpdate();
            var generator = worldBlockDatastore.GetBlock(generatorId).GetComponent<IGearGenerator>();
            var gear = worldBlockDatastore.GetBlock(targetGearId).GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }
    }
}
