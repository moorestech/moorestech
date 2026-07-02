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
    /// レール式延長プロトコルの異常系テスト。検証失敗時に状態が一切変化しないことを確認する
    /// Failure-path tests for the rail-style extend protocol; verifies no state mutation on validation failure
    /// </summary>
    public class ElectricWireExtendProtocolFailureTest
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
        public void 電線不足の延長は失敗し状態が一切変化しない()
        {
            // 必要数6に対して電線を3本しか持たない
            // Hold only three wires against the required six
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(4, 0, 0);
            var machinePos = new Vector3Int(6, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupInventory(poleCount: 1, wireCount: 3);
            var response = SendExtend(fromPos, newPolePos, PoleSlot);

            Assert.IsFalse(response.IsSuccess);
            Assert.IsFalse(worldBlockDatastore.Exists(newPolePos));
            Assert.AreEqual(1, CountItem(inventory, _poleItemId));
            Assert.AreEqual(3, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, machine.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 設置先が占有済みなら失敗し状態が変化しない()
        {
            // 設置先に別ブロックを先に置く
            // Occupy the target position with another block beforehand
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(4, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BlockId, newPolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = SetupInventory(poleCount: 1, wireCount: 10);
            var response = SendExtend(fromPos, newPolePos, PoleSlot);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(1, CountItem(inventory, _poleItemId));
            Assert.AreEqual(10, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 範囲外スロット指定は例外を出さず失敗応答を返す()
        {
            // 不正クライアント相当の範囲外スロットを送る
            // Send an out-of-range slot as a malicious client would
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(4, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);

            var inventory = SetupInventory(poleCount: 1, wireCount: 10);

            var negativeResponse = SendExtend(fromPos, newPolePos, -1);
            var overResponse = SendExtend(fromPos, newPolePos, 9999);

            Assert.IsFalse(negativeResponse.IsSuccess);
            Assert.IsFalse(overResponse.IsSuccess);
            Assert.AreEqual("NoPoleItem", negativeResponse.Error);
            Assert.AreEqual("NoPoleItem", overResponse.Error);
            Assert.IsFalse(worldBlockDatastore.Exists(newPolePos));
            Assert.AreEqual(1, CountItem(inventory, _poleItemId));
            Assert.AreEqual(10, CountItem(inventory, _wireItemId));
            Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        #region TestUtil

        private IOpenableInventory SetupInventory(int poleCount, int wireCount)
        {
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(PoleSlot, ServerContext.ItemStackFactory.Create(_poleItemId, poleCount));
            if (wireCount > 0) inventory.SetItem(WireSlot, ServerContext.ItemStackFactory.Create(_wireItemId, wireCount));
            return inventory;
        }

        private ElectricWireExtendProtocol.ElectricWireExtendResponse SendExtend(Vector3Int fromPos, Vector3Int newPolePos, int poleSlot)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(PlayerId, fromPos, poleSlot, placeInfo, _wireItemId));
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
