using Core.Update;
using System;
using System.Collections.Generic;
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
using Game.UnlockState;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ElectricWireAutoConnectIsolationTest : ElectricWireAutoConnectPlaceTestBase
    {

        [Test]
        public void 範囲内に接続先が無ければ電線消費なしで孤立設置される()
        {
            // 周囲に電気系ブロックが無い状態で機械を置く。電線ゼロでも成功する
            // Place a machine with no electric blocks nearby; succeeds even with zero wire items
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            PlaceBlock(packet, ForUnitTestModBlockId.MachineId, new Vector3Int(50, 0, 50));

            Assert.IsTrue(worldBlockDatastore.Exists(new Vector3Int(50, 0, 50)));

            var machine = worldBlockDatastore.GetBlock(new Vector3Int(50, 0, 50));
            Assert.AreEqual(0, machine.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void クリーンルーム機械も電柱の自動接続対象になる()
        {
            // 電柱の機械範囲(±2)内にクリーンルーム機械を置いてから電柱をプロトコル経由で設置する
            // Place a clean room machine within the pole's machine range (+-2), then place the pole via the protocol
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var cleanRoomMachine);

            SetupWire(serviceProvider, 5);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));

            // 旧resolverはCleanRoom未対応で自動接続されなかった。新resolverで接続されることを確認する
            // The old resolver skipped CleanRoom blocks; verify the new resolver wires them up
            var pole = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            var poleConnector = pole.GetComponent<IElectricWireConnector>();
            var machineConnector = cleanRoomMachine.GetComponent<IElectricWireConnector>();

            Assert.IsTrue(poleConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(poleConnector.BlockInstanceId));
        }

        [Test]
        public void 既に電柱と接続済みの機械は別の電柱設置で再接続されない()
        {
            // 電柱A接続済み機械の範囲内に電柱Bを設置
            // Connect machine to pole A, then place pole B in its range
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            SetupWire(serviceProvider, 10);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(1, 0, 0));

            var poleA = worldBlockDatastore.GetBlock(new Vector3Int(1, 0, 0));
            var machineConnector = machine.GetComponent<IElectricWireConnector>();
            var poleAConnector = poleA.GetComponent<IElectricWireConnector>();

            // 前提: 電柱A接続で機械の接続数=1
            // Precondition: auto-connect to pole A set connection count to 1
            Assert.IsTrue(machineConnector.ContainsWireConnection(poleAConnector.BlockInstanceId));
            Assert.AreEqual(1, machineConnector.WireConnections.Count);

            // 機械の上限は2なので、接続数を常に0とみなす実装だと容量フィルタだけでは弾けない配置
            // The machine's cap is 2, so an implementation that always reports 0 connections would slip past the capacity filter alone
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, -1));

            var poleB = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, -1));
            var poleBConnector = poleB.GetComponent<IElectricWireConnector>();

            // 電柱Bは電柱A接続可、機械へ再接続不可
            // Pole B may connect to pole A but must not reconnect to the machine
            Assert.IsTrue(poleAConnector.ContainsWireConnection(poleBConnector.BlockInstanceId));
            Assert.IsFalse(machineConnector.ContainsWireConnection(poleBConnector.BlockInstanceId));
            Assert.AreEqual(1, machineConnector.WireConnections.Count);
        }
    }
}
