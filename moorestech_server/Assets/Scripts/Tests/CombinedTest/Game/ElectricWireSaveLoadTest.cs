using static Tests.CombinedTest.Game.Helpers.ElectricWireSaveLoadTestHelpers;
using System;
using System.Linq;
using Core.Inventory;
using Core.Update;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Tests.CombinedTest.Game
{
    // ワイヤーのセーブ復元と撤去時の切断・返却を検証
    // Verify wire connections survive save/load and disconnect/refund correctly when a block is removed
    public class ElectricWireSaveLoadTest
    {
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 電柱-発電機-機械をワイヤー接続で消費ありのTryConnectを使って結ぶ→セーブ→別ワールドへロード
        // → 双方向接続・セグメント数・統計・切断時返却コストがすべて一致することを検証
        // Wire pole-generator-machine with consumption-based TryConnect, save, reload into a fresh world
        // → verify bidirectional connections, segment count, statistics and disconnect refund cost all match
        [Test]
        public void ワイヤー接続がセーブロードで復元される()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(saveServiceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            saveServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            // pole-machine有効範囲は±2なのでposGeneratorはその境界(距離2)に、
            // machine-machine有効範囲は±4なのでposMachineは境界(距離4)に配置する
            // Pole-machine effective range is +-2 so posGenerator sits at that boundary (distance 2);
            // machine-machine effective range is +-4 so posMachine sits at its boundary (distance 4)
            var posPole = Pos(0, 0);
            var posGenerator = Pos(2, 0);
            var posMachine = Pos(6, 0);

            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            ServerContext.WorldBlockDatastore.TryAddBlock(GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            ServerContext.WorldBlockDatastore.TryAddBlock(MachineId, posMachine, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(wireItemId, 10));

            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, playerId, ConnectToolGuid, false, out var errorA), errorA.ToString());
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posGenerator, posMachine, playerId, ConnectToolGuid, false, out var errorB), errorB.ToString());

            // トポロジ反映と統計確定のため1tick進める
            // Advance one tick for the topology flush and statistics settlement
            GameUpdater.UpdateOneTick();

            var networkDatastore = saveServiceProvider.GetService<IElectricWireNetworkLookup>();
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole.BlockInstanceId, out var savedSegment));
            var savedStatistics = savedSegment.Statistics;
            var savedRecord = pole.GetComponent<IElectricWireConnector>().WireConnections[generator.BlockInstanceId].Record;

            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // ロード直後は接続情報だけがあり、派生した電力網はまだ構築されていない
            // Immediately after load, connections exist but the derived electric network is not built yet
            var loadedNetworkDatastore = loadServiceProvider.GetService<IElectricWireNetworkLookup>();
            var loadedPoleBeforeRebuild = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            Assert.IsFalse(loadedNetworkDatastore.TryGetEnergySegment(loadedPoleBeforeRebuild.BlockInstanceId, out _));

            // ロード直後のトポロジ反映と統計確定のため1tick進める
            // Advance one tick after load for the topology flush and statistics settlement
            GameUpdater.UpdateOneTick();

            var loadedPole = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            var loadedGenerator = ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>();
            var loadedMachine = ServerContext.WorldBlockDatastore.GetBlock(posMachine).GetComponent<IElectricWireConnector>();

            // 双方向の接続関係が復元されていること
            // Bidirectional connections are restored
            Assert.IsTrue(loadedPole.ContainsWireConnection(loadedGenerator.BlockInstanceId));
            Assert.IsTrue(loadedGenerator.ContainsWireConnection(loadedPole.BlockInstanceId));
            Assert.IsTrue(loadedGenerator.ContainsWireConnection(loadedMachine.BlockInstanceId));
            Assert.IsTrue(loadedMachine.ContainsWireConnection(loadedGenerator.BlockInstanceId));
            Assert.IsFalse(loadedPole.ContainsWireConnection(loadedMachine.BlockInstanceId));

            Assert.AreEqual(1, GetSegmentCount(loadedNetworkDatastore));
            Assert.IsTrue(loadedNetworkDatastore.TryGetEnergySegment(loadedPole.BlockInstanceId, out var loadedSegment));
            Assert.IsTrue(loadedNetworkDatastore.TryGetEnergySegment(loadedGenerator.BlockInstanceId, out var loadedGeneratorSegment));
            Assert.IsTrue(loadedNetworkDatastore.TryGetEnergySegment(loadedMachine.BlockInstanceId, out var loadedMachineSegment));
            Assert.AreSame(loadedSegment, loadedGeneratorSegment);
            Assert.AreSame(loadedSegment, loadedMachineSegment);
            Assert.AreEqual(1, GetGenerators(loadedSegment).Count);
            Assert.AreEqual(1, GetConsumers(loadedSegment).Count);

            var loadedStatistics = loadedSegment.Statistics;
            Assert.AreEqual(savedStatistics.TotalGeneratePower, loadedStatistics.TotalGeneratePower);
            Assert.AreEqual(savedStatistics.TotalRequiredPower, loadedStatistics.TotalRequiredPower);
            Assert.AreEqual(savedStatistics.PowerRate, loadedStatistics.PowerRate);
            Assert.AreEqual(savedStatistics.ConsumerCount, loadedStatistics.ConsumerCount);

            // GUIDを介してコストが正しく復元されているか（保存時と同一）を確認する
            // Verify the connection cost (via GUID roundtrip) matches the pre-save value
            var loadedRecord = loadedPole.WireConnections[loadedGenerator.BlockInstanceId].Record;
            CollectionAssert.AreEqual(savedRecord.Materials, loadedRecord.Materials);
            Assert.AreEqual(savedRecord.ConnectToolGuid, loadedRecord.ConnectToolGuid);

            // 復元後の切断でセーブ前と同じコストが返却される
            // Disconnecting after restore refunds the same wire cost as before the save
            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var beforeDisconnectCount = CountItem(loadedInventory, wireItemId);
            Assert.IsTrue(ElectricWireDisconnectUtil.TryDisconnect(posPole, posGenerator, playerId, out var disconnectError), disconnectError.ToString());
            var afterDisconnectCount = CountItem(loadedInventory, wireItemId);
            Assert.AreEqual(savedRecord.TotalCount, afterDisconnectCount - beforeDisconnectCount);
        }



    }
}
