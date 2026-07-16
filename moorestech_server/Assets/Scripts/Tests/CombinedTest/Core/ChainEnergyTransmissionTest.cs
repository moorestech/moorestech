using System;
using Core.Update;
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
            GameUpdater.UpdateOneTick();

            // 発電機とターゲットギアの回転が一致することを確認する
            // Ensure generator and target gear share rpm and direction
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            var gear = targetGear.GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }

        [Test]
        public void RemoveChainConnectedPoleDoesNotThrow()
        {
            // テスト用DIコンテナを立ち上げる
            // Initialize test DI container
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 0;
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            // 3本のチェーンポールを直列に配置する
            // Place three chain poles in a line
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = new Vector3Int(1, 0, 0);
            var posB = new Vector3Int(4, 0, 0);
            var posC = new Vector3Int(7, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleC);

            // プレイヤーにチェーンを付与し、A-B / B-C を接続する
            // Grant chain items and connect A-B / B-C
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 20));
            Assert.True(GearChainSystemUtil.TryConnect(posA, posB, playerId, chainItemId, out _));
            Assert.True(GearChainSystemUtil.TryConnect(posB, posC, playerId, chainItemId, out _));

            var poleAId = poleA.BlockInstanceId;
            var poleBId = poleB.BlockInstanceId;
            var poleCId = poleC.BlockInstanceId;

            // 他ポールに接続された中央ポールを削除しても例外が出ないことを確認する
            // Removing the chain-connected middle pole must not throw
            Assert.DoesNotThrow(() => worldBlockDatastore.RemoveBlock(posB, BlockRemoveReason.ManualRemove));

            // 中央ポールが消え、両端ポールから接続が消えていることを確認する
            // Verify the middle pole is gone and the connection is cleared from both ends
            Assert.IsNull(worldBlockDatastore.GetBlock(poleBId));
            Assert.IsFalse(worldBlockDatastore.GetBlock(poleAId).GetComponent<IGearChainPole>().ContainsChainConnection(poleBId));
            Assert.IsFalse(worldBlockDatastore.GetBlock(poleCId).GetComponent<IGearChainPole>().ContainsChainConnection(poleBId));

            // 残ったネットワークの更新でも例外が出ないことを確認する
            // Updating the surviving networks must not throw either
            Assert.DoesNotThrow(GameUpdater.UpdateOneTick);
        }

        [Test]
        public void RemoveChainPoleBridgingGearMeshDoesNotThrow()
        {
            // テスト用DIコンテナを立ち上げる
            // Initialize test DI container
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 0;
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            // 発電機-poleA(隣接ギア噛み合い)-チェーン-poleB-ギア のネットワークを構築する
            // Build network: generator-poleA(gear-meshed)-chain-poleB-gear
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var generatorPos = new Vector3Int(0, 0, 0);
            var poleAPos = new Vector3Int(1, 0, 0);
            var poleBPos = new Vector3Int(6, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, poleAPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, poleBPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(7, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // poleA-poleB をチェーン接続する
            // Connect poleA-poleB with chain
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 20));
            Assert.True(GearChainSystemUtil.TryConnect(poleAPos, poleBPos, playerId, chainItemId, out _));

            // 隣接ギアと噛み合いつつチェーンで橋渡しするpoleAを削除しても例外が出ないことを確認する
            // Removing poleA, which both gear-meshes and chain-bridges, must not throw
            Assert.DoesNotThrow(() => worldBlockDatastore.RemoveBlock(poleAPos, BlockRemoveReason.ManualRemove));

            // 削除後のネットワーク更新でも例外が出ないことを確認する
            // Updating networks after removal must not throw either
            Assert.DoesNotThrow(GameUpdater.UpdateOneTick);
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
            GameUpdater.UpdateOneTick();

            worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var loadedPoleA = worldBlockDatastore.GetBlock(poleAId).GetComponent<IGearChainPole>();
            var loadedPoleB = worldBlockDatastore.GetBlock(poleBId).GetComponent<IGearChainPole>();
            Assert.IsTrue(loadedPoleA.ContainsChainConnection(poleBId));
            Assert.IsTrue(loadedPoleB.ContainsChainConnection(poleAId));

            // チェーン接続先を含めた単一ネットワークへ統合されていることを確認する
            // Ensure the chain-connected endpoints are merged into one network
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(generatorId, out var generatorNetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(poleAId, out var poleANetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(poleBId, out var poleBNetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(targetGearId, out var targetGearNetwork));
            Assert.AreSame(generatorNetwork, poleANetwork);
            Assert.AreSame(generatorNetwork, poleBNetwork);
            Assert.AreSame(generatorNetwork, targetGearNetwork);

            // ロード後の更新で発電機の回転がチェーン先へ届くことを確認する
            // Verify generator rotation reaches the far side after load update
            var generator = worldBlockDatastore.GetBlock(generatorId).GetComponent<IGearGenerator>();
            var gear = worldBlockDatastore.GetBlock(targetGearId).GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }
    }
}
