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
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Tests.CombinedTest.Game
{
    // ワイヤーのセーブ復元と撤去時の切断・返却を検証
    // Verify wire connections survive save/load and disconnect/refund correctly when a block is removed
    public class ElectricWireSaveLoadTest
    {
        private const int PlayerId = 5;
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
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            saveServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            var posPole = Pos(0, 0);
            var posGenerator = Pos(3, 0);
            var posMachine = Pos(6, 0);

            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            ServerContext.WorldBlockDatastore.TryAddBlock(GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            ServerContext.WorldBlockDatastore.TryAddBlock(MachineId, posMachine, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(wireItemId, 10));

            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, PlayerId, ConnectToolGuid, out var errorA), errorA.ToString());
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posGenerator, posMachine, PlayerId, ConnectToolGuid, out var errorB), errorB.ToString());

            // トポロジ反映と統計確定のため1tick進める
            // Advance one tick for the topology flush and statistics settlement
            GameUpdater.UpdateOneTick();

            var networkDatastore = saveServiceProvider.GetService<IElectricWireNetworkDatastore>();
            Assert.AreEqual(1, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole.BlockInstanceId, out var savedSegment));
            var savedStatistics = savedSegment.Statistics;
            var savedCost = pole.GetComponent<IElectricWireConnector>().WireConnections[generator.BlockInstanceId].Cost;

            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

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

            var loadedNetworkDatastore = loadServiceProvider.GetService<IElectricWireNetworkDatastore>();
            Assert.AreEqual(1, loadedNetworkDatastore.SegmentCount);
            Assert.IsTrue(loadedNetworkDatastore.TryGetEnergySegment(loadedPole.BlockInstanceId, out var loadedSegment));
            // 役割ごとのメンバー数(電柱=Transformer、発電機=Generator、機械=Consumer)を確認
            // Verify per-role membership counts (pole=Transformer, generator=Generator, machine=Consumer)
            Assert.AreEqual(1, loadedSegment.EnergyTransformers.Count);
            Assert.AreEqual(1, loadedSegment.Generators.Count);
            Assert.AreEqual(1, loadedSegment.Consumers.Count);

            var loadedStatistics = loadedSegment.Statistics;
            Assert.AreEqual(savedStatistics.TotalGeneratePower, loadedStatistics.TotalGeneratePower);
            Assert.AreEqual(savedStatistics.TotalRequiredPower, loadedStatistics.TotalRequiredPower);
            Assert.AreEqual(savedStatistics.PowerRate, loadedStatistics.PowerRate);
            Assert.AreEqual(savedStatistics.ConsumerCount, loadedStatistics.ConsumerCount);

            // GUIDを介してコストが正しく復元されているか（保存時と同一）を確認する
            // Verify the connection cost (via GUID roundtrip) matches the pre-save value
            var loadedCost = loadedPole.WireConnections[loadedGenerator.BlockInstanceId].Cost;
            CollectionAssert.AreEqual(savedCost.Materials, loadedCost.Materials);

            // 復元後の切断でセーブ前と同じコストが返却される
            // Disconnecting after restore refunds the same wire cost as before the save
            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var beforeDisconnectCount = CountItem(loadedInventory, wireItemId);
            Assert.IsTrue(ElectricWireSystemUtil.TryDisconnect(posPole, posGenerator, PlayerId, out var disconnectError), disconnectError.ToString());
            var afterDisconnectCount = CountItem(loadedInventory, wireItemId);
            Assert.AreEqual(savedCost.TotalCount, afterDisconnectCount - beforeDisconnectCount);
        }

        // 3ブロックを鎖状に接続後、中央を撤去するとセグメントが分割され、
        // 相手側のWireConnectionsがクリアされ、GetRefundItemsに距離分の電線コストが含まれることを検証
        // After chaining three blocks, removing the middle splits the segment, clears the partners'
        // WireConnections, and GetRefundItems reports the wire cost proportional to each removed connection's distance
        [Test]
        public void ブロック撤去でワイヤーが切れ電線が返却される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            var posA = Pos(0, 0);
            var posB = Pos(3, 0);
            var posC = Pos(6, 0);

            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);
            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockC);

            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(wireItemId, 10));

            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var errorA), errorA.ToString());
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posB, posC, PlayerId, ConnectToolGuid, out var errorB), errorB.ToString());

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();
            Assert.AreEqual(1, networkDatastore.SegmentCount);

            var connectorA = blockA.GetComponent<IElectricWireConnector>();
            var connectorB = blockB.GetComponent<IElectricWireConnector>();
            var connectorC = blockC.GetComponent<IElectricWireConnector>();

            // 配置座標(0,0)-(3,0)-(6,0)より各接続の距離は3。consumptionPerLength=1なので電線コストは3本ずつ
            // Blocks at (0,0)-(3,0)-(6,0) put each connection at distance 3; with consumptionPerLength=1 each wire costs 3
            var costToA = connectorB.WireConnections[connectorA.BlockInstanceId].Cost;
            var costToC = connectorB.WireConnections[connectorC.BlockInstanceId].Cost;
            Assert.AreEqual(3, costToA.TotalCount);
            Assert.AreEqual(3, costToC.TotalCount);

            var refundItems = blockB.GetComponent<IGetRefundItemsInfo>().GetRefundItems();
            Assert.AreEqual(2, refundItems.Count);
            Assert.IsTrue(refundItems.All(item => item.Id == wireItemId));
            Assert.AreEqual(6, refundItems.Sum(item => item.Count));

            ServerContext.WorldBlockDatastore.RemoveBlock(posB, BlockRemoveReason.ManualRemove);

            // 撤去に伴うトポロジ反映のため1tick進める
            // Advance one tick so the removal's topology flush is applied
            GameUpdater.UpdateOneTick();

            // 相手側のWireConnectionsから中央ブロックが消えていること
            // The middle block is gone from both partners' WireConnections
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsFalse(connectorC.ContainsWireConnection(connectorB.BlockInstanceId));

            // セグメントがA単独・C単独の2つに分かれていること
            // The segment splits into two: A alone and C alone
            Assert.AreEqual(2, networkDatastore.SegmentCount);
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(connectorA.BlockInstanceId, out var segmentA));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(connectorC.BlockInstanceId, out var segmentC));
            Assert.AreNotSame(segmentA, segmentC);
            Assert.AreEqual(1, segmentA.EnergyTransformers.Count);
            Assert.AreEqual(1, segmentC.EnergyTransformers.Count);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }

        private static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }
    }
}
