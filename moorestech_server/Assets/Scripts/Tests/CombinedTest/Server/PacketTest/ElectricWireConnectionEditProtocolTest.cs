using System;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ElectricWireConnectionEditProtocolTest
    {
        private const int PlayerId = 7;
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001");

        private ServiceProvider _serviceProvider;
        private PacketResponseCreator _packet;
        private ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packet = packet;
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
        }

        [Test]
        public void 接続成功で電線が消費されセグメントがマージされる()
        {
            // 距離3の2本の電柱を設置し、電線5本を配布する
            // Place two poles 3 apart and give five wire items
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(5);
            var networkDatastore = _serviceProvider.GetService<IElectricWireNetworkDatastore>();
            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(2, networkDatastore.SegmentCount);

            // 接続プロトコルを送信する
            // Send the connect protocol
            var response = SendConnect(posA, posB);

            Assert.IsTrue(response.IsSuccess);
            Assert.IsTrue(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsTrue(connectorB.ContainsWireConnection(connectorA.BlockInstanceId));
            Assert.AreEqual(2, CountItem(inventory, _wireItemId));
            // 接続反映のため1tick進める
            // Advance one tick so the connection is applied
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, networkDatastore.SegmentCount);
        }

        [Test]
        public void 切断で電線が返却される()
        {
            // 接続してから切断し、電線が戻ることを確認する
            // Connect then disconnect and verify wire refund
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(5);

            SendConnect(posA, posB);
            Assert.AreEqual(2, CountItem(inventory, _wireItemId));

            var response = SendDisconnect(posA, posB);

            Assert.IsTrue(response.IsSuccess);
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsFalse(connectorB.ContainsWireConnection(connectorA.BlockInstanceId));
            Assert.AreEqual(5, CountItem(inventory, _wireItemId));
        }

        [Test]
        public void 未接続の切断はNotConnectedで失敗する()
        {
            // 接続していない2本の電柱を切断しようとする
            // Attempt to disconnect two poles that are not connected
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            PlaceTwoPoles(posA, posB);

            var response = SendDisconnect(posA, posB);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NotConnected, response.FailureReason);
        }

        [Test]
        public void 電線不足で接続に失敗する()
        {
            // 電線を持たずに接続を試みる
            // Attempt connection while holding no wire items
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);

            var response = SendConnect(posA, posB);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, response.FailureReason);
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
        }

        #region TestUtil

        private (IElectricWireConnector, IElectricWireConnector) PlaceTwoPoles(Vector3Int posA, Vector3Int posB)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);
            return (blockA.GetComponent<IElectricWireConnector>(), blockB.GetComponent<IElectricWireConnector>());
        }

        private IOpenableInventory GiveWire(int count)
        {
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_wireItemId, count));
            return inventory;
        }

        private ElectricWireConnectionEditProtocol.ElectricWireConnectionEditResponse SendConnect(Vector3Int posA, Vector3Int posB)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireConnectionEditProtocol.ElectricWireConnectionEditRequest.CreateConnectRequest(posA, posB, PlayerId, _wireItemId));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext());
            return MessagePackSerializer.Deserialize<ElectricWireConnectionEditProtocol.ElectricWireConnectionEditResponse>(responses[0]);
        }

        private ElectricWireConnectionEditProtocol.ElectricWireConnectionEditResponse SendDisconnect(Vector3Int posA, Vector3Int posB)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireConnectionEditProtocol.ElectricWireConnectionEditRequest.CreateDisconnectRequest(posA, posB, PlayerId));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext());
            return MessagePackSerializer.Deserialize<ElectricWireConnectionEditProtocol.ElectricWireConnectionEditResponse>(responses[0]);
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
