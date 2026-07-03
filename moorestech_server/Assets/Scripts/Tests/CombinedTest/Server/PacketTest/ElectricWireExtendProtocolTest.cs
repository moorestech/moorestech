using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// レール式延長プロトコルの正常系テスト。異常系はElectricWireExtendProtocolFailureTest参照
    /// Success-path tests for the rail-style extend protocol; see ElectricWireExtendProtocolFailureTest for failures
    /// </summary>
    public class ElectricWireExtendProtocolTest
    {
        private const int PlayerId = 9;
        private const int PoleSlot = 3;
        private const int WireSlot = 4;
        private static readonly Guid PoleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001");

        private ServiceProvider _serviceProvider;
        private PacketResponseCreator _packet;
        private ItemId _poleItemId;
        private ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packet = packet;
            _poleItemId = MasterHolder.ItemMaster.GetItemId(PoleItemGuid);
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
        }

        [Test]
        public void 起点あり延長で電柱を設置し起点と機械へ接続して消費する()
        {
            // 起点電柱と未接続機械を用意する
            // Prepare an origin pole and an unconnected machine
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(4, 0, 0);
            var machinePos = new Vector3Int(6, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupInventory(poleCount: 1, wireCount: 10);
            var fromConnector = fromPole.GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();

            // 起点あり延長を実行する（起点距離4＋機械距離2＝電線6）
            // Run extend with origin (origin distance 4 + machine distance 2 = 6 wires)
            var response = SendExtend(fromPos, newPolePos);

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.IsTrue(worldBlockDatastore.Exists(newPolePos));

            var newPole = worldBlockDatastore.GetBlock(newPolePos);
            var newConnector = newPole.GetComponent<IElectricWireConnector>();

            Assert.AreEqual(newPolePos, (Vector3Int)response.PlacedPolePos);
            Assert.AreEqual(newConnector.BlockInstanceId.AsPrimitive(), response.PlacedBlockInstanceId);
            Assert.IsTrue(fromConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.IsTrue(newConnector.ContainsWireConnection(fromConnector.BlockInstanceId));
            Assert.IsTrue(newConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.AreEqual(2, newConnector.WireConnections.Count);
            Assert.AreEqual(4, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, CountItem(inventory, _poleItemId));
        }

        [Test]
        public void 起点なし孤立設置は電線消費なしで電柱のみ設置する()
        {
            // 周囲に何も無い空きスペースへ起点なしで電柱を設置する
            // Place a pole without origin in empty space with nothing nearby
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var newPolePos = new Vector3Int(50, 0, 50);
            var inventory = SetupInventory(poleCount: 1, wireCount: 0);

            var response = SendIsolatedPlace(newPolePos);

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.IsTrue(worldBlockDatastore.Exists(newPolePos));
            Assert.AreEqual(0, CountItem(inventory, _poleItemId));

            var newPole = worldBlockDatastore.GetBlock(newPolePos);
            Assert.AreEqual(0, newPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 起点なし設置でも近傍電柱へ通常設置と同様に自動接続される()
        {
            // 既存電柱の探索範囲内（poleConnectionRange=7は±3）へ起点なしで電柱を設置する
            // Place a pole without origin inside the existing pole's search range (poleConnectionRange=7 means +-3)
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var existingPolePos = Vector3Int.zero;
            var newPolePos = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, existingPolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var existingPole);

            var inventory = SetupInventory(poleCount: 1, wireCount: 10);
            var response = SendIsolatedPlace(newPolePos);

            Assert.IsTrue(response.IsSuccess, response.Error);

            // 最寄り電柱1本へ自動接続され、距離3の電線が消費される
            // Auto-connects to the nearest pole and consumes 3 wires for distance 3
            var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();
            var existingConnector = existingPole.GetComponent<IElectricWireConnector>();
            Assert.IsTrue(newConnector.ContainsWireConnection(existingConnector.BlockInstanceId));
            Assert.IsTrue(existingConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.AreEqual(7, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, CountItem(inventory, _poleItemId));
        }

        #region TestUtil

        private IOpenableInventory SetupInventory(int poleCount, int wireCount)
        {
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(PoleSlot, ServerContext.ItemStackFactory.Create(_poleItemId, poleCount));
            if (0 < wireCount) inventory.SetItem(WireSlot, ServerContext.ItemStackFactory.Create(_wireItemId, wireCount));
            return inventory;
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendExtend(Vector3Int fromPos, Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(PlayerId, fromPos, PoleSlot, placeInfo, _wireItemId));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext());
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendIsolatedPlace(Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateIsolatedPlaceRequest(PlayerId, PoleSlot, placeInfo, _wireItemId));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext());
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        private static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }

        #endregion
    }
}
