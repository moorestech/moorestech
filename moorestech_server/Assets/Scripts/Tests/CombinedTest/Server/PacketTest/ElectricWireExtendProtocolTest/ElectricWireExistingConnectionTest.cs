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
    public class ElectricWireExistingConnectionTest : ElectricWireExtendProtocolTestBase
    {

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
    }
}
