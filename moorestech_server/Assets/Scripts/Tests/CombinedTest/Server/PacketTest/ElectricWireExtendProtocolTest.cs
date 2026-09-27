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
    public class ElectricWireExtendProtocolTest : ElectricWireExtendProtocolTestBase
    {

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

            // 終点は新設電柱そのもので、次の起点として座標とInstanceIdが返る
            // The endpoint is the newly placed pole itself, returned as the next origin position and InstanceId
            Assert.AreEqual(newPolePos, (Vector3Int)response.EndpointPos);
            Assert.AreEqual(newConnector.BlockInstanceId.AsPrimitive(), response.EndpointBlockInstanceId);

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

            // 終点は孤立設置した電柱そのもので、次の起点として座標とInstanceIdが返る
            // The endpoint is the isolated pole itself, returned as the next origin position and InstanceId
            var newPole = worldBlockDatastore.GetBlock(newPolePos);
            Assert.AreEqual(0, newPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(newPolePos, (Vector3Int)response.EndpointPos);
            Assert.AreEqual(newPole.GetComponent<IElectricWireConnector>().BlockInstanceId.AsPrimitive(), response.EndpointBlockInstanceId);
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
        public void 機械を起点にした延長は機械との1本のみ接続し電線を距離分だけ消費する()
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
    }
}
