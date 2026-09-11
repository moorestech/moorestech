using System;
using Common.Debug;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Protocol;
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
            var (packet, serviceProvider) = CreateServer();
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
