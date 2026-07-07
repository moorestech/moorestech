using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// ElectricWireSystemUtilの結合テスト。実ブロックを設置してTryConnect/TryDisconnectを検証する
    /// Integration tests for ElectricWireSystemUtil, placing real blocks to drive TryConnect/TryDisconnect
    /// </summary>
    public class ElectricWireSystemUtilTest
    {
        private const int PlayerId = 3;
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001");

        private ServiceProvider _serviceProvider;
        private ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
        }

        [Test]
        public void 距離が離れすぎると接続に失敗する()
        {
            // maxWireLength(12)を超える距離に電柱を置く
            // Place poles farther apart than maxWireLength (12)
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(30, 0, 0);
            PlaceTwoPoles(posA, posB);
            GiveWire(50);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, _wireItemId, out var error);

            Assert.IsFalse(connected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.TooFar, error);
        }

        [Test]
        public void 電線不足で接続に失敗し状態が変化しない()
        {
            // 距離3に対して電線を2本しか持たない
            // Hold only two wires for a distance-3 connection
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(2);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, _wireItemId, out var error);

            Assert.IsFalse(connected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, error);
            Assert.AreEqual(2, CountItem(inventory, _wireItemId));
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
        }

        [Test]
        public void 接続で電線を消費し双方向登録される()
        {
            // 距離3の電柱を接続する
            // Connect two poles 3 apart
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(5);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, _wireItemId, out var error);

            Assert.IsTrue(connected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.None, error);
            Assert.IsTrue(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsTrue(connectorB.ContainsWireConnection(connectorA.BlockInstanceId));
            Assert.AreEqual(2, CountItem(inventory, _wireItemId));
        }

        [Test]
        public void 切断で電線を返却し接続が消える()
        {
            // 接続してから切断する
            // Connect and then disconnect
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            var inventory = GiveWire(5);

            ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, _wireItemId, out _);
            var disconnected = ElectricWireSystemUtil.TryDisconnect(posA, posB, PlayerId, out var error);

            Assert.IsTrue(disconnected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.None, error);
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsFalse(connectorB.ContainsWireConnection(connectorA.BlockInstanceId));
            Assert.AreEqual(5, CountItem(inventory, _wireItemId));
        }

        [Test]
        public void 未接続の切断はNotConnectedで失敗する()
        {
            // 接続していない電柱を切断しようとする
            // Attempt to disconnect poles that are not connected
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            PlaceTwoPoles(posA, posB);

            var disconnected = ElectricWireSystemUtil.TryDisconnect(posA, posB, PlayerId, out var error);

            Assert.IsFalse(disconnected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NotConnected, error);
        }

        #region TestUtil

        private static (IElectricWireConnector, IElectricWireConnector) PlaceTwoPoles(Vector3Int posA, Vector3Int posB)
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
