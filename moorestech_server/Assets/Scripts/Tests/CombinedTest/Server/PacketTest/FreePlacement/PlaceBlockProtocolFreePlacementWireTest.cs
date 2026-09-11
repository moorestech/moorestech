using System;
using System.Linq;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest.FreePlacement
{
    /// <summary>
    /// 無料設置デバッグONでも電線が自動接続され、素材は消費されないことを検証する（ADR 0056）
    /// Verifies free-placement debug still auto-connects wires without consuming materials (ADR 0056)
    /// </summary>
    public class PlaceBlockProtocolFreePlacementWireTest
    {
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void SetUp()
        {
            // Tests は ServerTestsDebugParametersIsolationFixture で隔離済み。ここではONにするだけ
            // Tests are isolated by ServerTestsDebugParametersIsolationFixture; only turn the flag on here
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
        }

        [TearDown]
        public void TearDown()
        {
            // 後続テストへ無料設置を残さない
            // Never leak free placement into later tests
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
        }

        [Test]
        public void 無料設置ONなら未解放かつ電線0でも電柱が機械へ自動接続される()
        {
            var (packet, _) = CreateServer();
            var datastore = ServerContext.WorldBlockDatastore;

            // 機械を先に置き、ブロック・connectToolとも未解放・電線0のまま電柱を無料設置する
            // Place a machine first, then free-place a pole with block and connectTool both locked and zero wire
            datastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (1, 0)), new PacketResponseContext(null));

            var pole = datastore.GetBlock(new Vector3Int(1, 0, 0));
            Assert.IsNotNull(pole);
            var poleConnector = pole.GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();
            Assert.IsTrue(poleConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(poleConnector.BlockInstanceId));
        }

        [Test]
        public void 無料設置ONなら電線を所持していても消費されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var datastore = ServerContext.WorldBlockDatastore;
            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 10, WireItemGuid, 5);

            datastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (1, 0)), new PacketResponseContext(null));

            // 接続はされ、電線は5個のまま
            // The wire is connected and the 5 wire items stay untouched
            var poleConnector = datastore.GetBlock(new Vector3Int(1, 0, 0)).GetComponent<IElectricWireConnector>();
            Assert.IsTrue(poleConnector.ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(5, GetItemCount(inventory, WireItemGuid));
        }

        [Test]
        public void 無料設置で張った電線は撤去でも切断でも素材を返却しない()
        {
            var (packet, serviceProvider) = CreateServer();
            var datastore = ServerContext.WorldBlockDatastore;
            var inventory = GetInventory(serviceProvider);
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);

            datastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (1, 0)), new PacketResponseContext(null));
            var pole = datastore.GetBlock(new Vector3Int(1, 0, 0));
            Assert.IsTrue(pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));

            // 撤去時の返却品（両端とも）に電線が含まれない
            // Neither end's removal refund contains the wire item
            Assert.IsFalse(pole.GetComponent<IGetRefundItemsInfo>().GetRefundItems().Any(item => item.Id == wireItemId));
            Assert.IsFalse(machine.GetComponent<IGetRefundItemsInfo>().GetRefundItems().Any(item => item.Id == wireItemId));

            // 切断は成功するが、電線はインベントリへ湧かない
            // Disconnecting succeeds, yet no wire appears in the inventory
            Assert.IsTrue(ElectricWireSystemUtil.TryDisconnect(new Vector3Int(1, 0, 0), new Vector3Int(0, 0, 0), PlayerId, out var failureReason), failureReason.ToString());
            Assert.AreEqual(0, GetItemCount(inventory, WireItemGuid));
        }

        [Test]
        public void 無料設置ONで範囲内に接続先が無ければ孤立設置される()
        {
            var (packet, _) = CreateServer();

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (0, 0)), new PacketResponseContext(null));

            var pole = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsNotNull(pole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }
    }
}
