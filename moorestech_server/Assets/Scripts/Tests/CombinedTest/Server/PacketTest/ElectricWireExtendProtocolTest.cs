using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
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
        private const int MaterialSlot = 3;
        private const int WireSlot = 4;
        private static readonly Guid MaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000005"); // Test5 (電柱の建設コスト×1)
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid LockedConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002"); // SetUpで解放しない未解放ツール
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private ServiceProvider _serviceProvider;
        private PacketResponseCreator _packet;
        private ItemId _materialItemId;
        private ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packet = packet;
            _materialItemId = MasterHolder.ItemMaster.GetItemId(MaterialGuid);
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
        }

        [Test]
        public void 起点あり延長は起点との1本のみ接続し周辺機械へは配線しない()
        {
            // 起点電柱と、新電柱の機械範囲内の未接続機械を用意する
            // Prepare an origin pole and an unconnected machine inside the new pole's machine range
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(3, 0, 0);
            var machinePos = new Vector3Int(5, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupInventory(materialCount: 1, wireCount: 10);
            var fromConnector = fromPole.GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();

            // 起点あり延長を実行する（起点距離3の電線3本だけが消費される）
            // Run extend with origin; only 3 wires for the origin distance are consumed
            var response = SendExtend(fromPos, newPolePos);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
            var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();

            // 接続は起点との1本のみで、周辺機械へは配線されない
            // Exactly one edge to the origin; the nearby machine stays unwired
            Assert.AreEqual(1, newConnector.WireConnections.Count);
            Assert.IsTrue(fromConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.AreEqual(0, machineConnector.WireConnections.Count);
            Assert.AreEqual(7, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, CountItem(inventory, _materialItemId));
        }

        [Test]
        public void 起点なし孤立設置は電線消費なしで電柱のみ設置する()
        {
            // 空きスペースへ起点なしで電柱を設置する
            // Place a pole without origin in empty space with nothing nearby
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var newPolePos = new Vector3Int(50, 0, 50);
            var inventory = SetupInventory(materialCount: 1, wireCount: 0);

            var response = SendIsolatedPlace(newPolePos);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
            Assert.IsTrue(worldBlockDatastore.Exists(newPolePos));
            Assert.AreEqual(0, CountItem(inventory, _materialItemId));

            var newPole = worldBlockDatastore.GetBlock(newPolePos);
            Assert.AreEqual(0, newPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 起点なし孤立設置は近傍に電柱があっても一切接続しない()
        {
            // 既存電柱の探索範囲内へ起点なしで電柱を設置する
            // Place a pole without origin inside the existing pole's search range
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var existingPolePos = Vector3Int.zero;
            var newPolePos = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, existingPolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var existingPole);

            var inventory = SetupInventory(materialCount: 1, wireCount: 10);
            var response = SendIsolatedPlace(newPolePos);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());

            // 接続ゼロ・電線消費ゼロで電柱のみ設置される
            // The pole is placed alone: zero connections and zero wire consumption
            var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(0, newConnector.WireConnections.Count);
            Assert.AreEqual(0, existingPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(10, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, CountItem(inventory, _materialItemId));
        }

        [Test]
        public void 未接続機械を起点にした延長で二重接続や電線二重消費が起きない()
        {
            // 新電柱の機械範囲内にいる未接続機械そのものを起点にする（機械を起点にした延長の基本ケース）
            // Use an unconnected machine inside the new pole's machine range as the origin (basic case of extending from a machine)
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var machinePos = Vector3Int.zero;
            var newPolePos = new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            // 電線を必要数ちょうど（距離2＝2本）にし、二重計上なら検証で弾かれ失敗する
            // Hold exactly the needed wires (distance 2 = 2); double counting would fail the validation
            var inventory = SetupInventory(materialCount: 1, wireCount: 2);
            var response = SendExtend(machinePos, newPolePos);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());

            // 起点との接続が1本だけ残り、電線2本のみ消費される
            // Exactly one edge to the origin remains and only 2 wires are consumed
            var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();
            Assert.AreEqual(1, newConnector.WireConnections.Count);
            Assert.IsTrue(newConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.AreEqual(0, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, CountItem(inventory, _materialItemId));
        }

        [Test]
        public void 既存ブロック接続Operationで接続され終点InstanceIdが返る()
        {
            // 範囲内の電柱2本を用意して接続する
            // Prepare two poles in range and connect them
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var toPos = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, toPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var toPole);

            var inventory = SetupInventory(materialCount: 0, wireCount: 10);
            var response = SendConnect(fromPos, toPos, ConnectToolGuid);

            // 接続成功し、終点（接続先）のInstanceIdが次の起点として返る
            // Connection succeeds and the endpoint (target) InstanceId is returned as the next origin
            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
            var toConnector = toPole.GetComponent<IElectricWireConnector>();
            Assert.AreEqual(toPos, (Vector3Int)response.EndpointPos);
            Assert.AreEqual(toConnector.BlockInstanceId.AsPrimitive(), response.EndpointBlockInstanceId);
            Assert.IsTrue(fromPole.GetComponent<IElectricWireConnector>().ContainsWireConnection(toConnector.BlockInstanceId));
            Assert.AreEqual(7, CountItem(inventory, _wireItemId));
        }

        [Test]
        public void 既存ブロック接続Operationは電線不足で失敗し理由が返る()
        {
            // 電線を持たずに接続を要求する
            // Request a connection while holding no wires
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var toPos = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, toPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            SetupInventory(materialCount: 0, wireCount: 0);
            var response = SendConnect(fromPos, toPos, ConnectToolGuid);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, response.FailureReason);
            Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 既存ブロック接続Operationは未解放connectToolでNotUnlockedにより失敗し接続されない()
        {
            // 範囲内の電柱2本と十分な電線を用意するが、connectToolは未解放のままにする
            // Prepare two poles in range with enough wire, but leave the connectTool locked
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var toPos = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, toPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var toPole);

            SetupInventory(materialCount: 0, wireCount: 10);
            var response = SendConnect(fromPos, toPos, LockedConnectToolGuid);

            // 未解放理由で失敗し、双方とも接続が1本も張られない
            // Fails with the locked reason and neither side gains a connection
            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NotUnlocked, response.FailureReason);
            Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, toPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        #region TestUtil

        private IOpenableInventory SetupInventory(int materialCount, int wireCount)
        {
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            if (0 < materialCount) inventory.SetItem(MaterialSlot, ServerContext.ItemStackFactory.Create(_materialItemId, materialCount));
            if (0 < wireCount) inventory.SetItem(WireSlot, ServerContext.ItemStackFactory.Create(_wireItemId, wireCount));
            return inventory;
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendExtend(Vector3Int fromPos, Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(PlayerId, fromPos, ForUnitTestModBlockId.ElectricPoleId, placeInfo, ConnectToolGuid));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendIsolatedPlace(Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateIsolatedPlaceRequest(PlayerId, ForUnitTestModBlockId.ElectricPoleId, placeInfo));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(PlayerId, fromPos, toPos, connectToolGuid));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
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
