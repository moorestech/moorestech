using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
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
    public class ElectricWireDisconnectProtocolTest
    {
        private const int PlayerId = 7;
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

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
            _serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
        }

        [Test]
        public void 切断で電線が返却される()
        {
            // 延長プロトコルの接続Operationで接続してから切断し、電線が戻ることを確認する
            // Connect via the extend protocol's connect operation, then disconnect and verify wire refund
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(5);

            SendConnectViaExtend(posA, posB);
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

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendConnectViaExtend(Vector3Int posA, Vector3Int posB)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(PlayerId, posA, posB, ConnectToolGuid));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        private ElectricWireDisconnectProtocol.ElectricWireDisconnectResponse SendDisconnect(Vector3Int posA, Vector3Int posB)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(posA, posB, PlayerId));
            var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<ElectricWireDisconnectProtocol.ElectricWireDisconnectResponse>(responses[0]);
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
