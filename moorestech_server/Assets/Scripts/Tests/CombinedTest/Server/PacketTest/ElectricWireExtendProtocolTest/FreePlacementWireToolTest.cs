using System;
using System.IO;
using Common.Debug;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class FreePlacementWireToolTest : ElectricWireExtendProtocolTestBase
    {
        [SetUp]
        public void EnableFreePlacement()
        {
            // 開発者の永続デバッグ設定から隔離した上で無料設置をONにする
            // Isolate from the developer's persistent debug settings, then turn free placement on
            DebugParametersCacheDirectory.SetOverride(Path.Combine(Path.GetTempPath(), $"moorestech-free-placement-{TestContext.CurrentContext.Test.ID}"));
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
        }

        [TearDown]
        public void DisableFreePlacement()
        {
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            DebugParametersCacheDirectory.SetOverride(null);
        }

        [Test]
        public void 無料設置では素材無しで延長設置できコスト0で記録される()
        {
            // 建設コストも電線も持たずに起点電柱から延長する
            // Extend from an origin pole holding neither construction materials nor wires
            var fromPos = Vector3Int.zero;
            var newPolePos = new Vector3Int(3, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
            var inventory = SetupInventory(materialCount: 0, wireCount: 0);

            var response = SendExtend(fromPos, newPolePos);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
            var fromConnector = fromPole.GetComponent<IElectricWireConnector>();
            var newConnector = ServerContext.WorldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();
            Assert.IsTrue(fromConnector.ContainsWireConnection(newConnector.BlockInstanceId));
            Assert.AreEqual(0, fromConnector.WireConnections[newConnector.BlockInstanceId].Cost.TotalCount);
            Assert.AreEqual(0, CountItem(inventory, _wireItemId));
        }

        [Test]
        public void 無料設置では素材無しで既存ブロック同士を接続できる()
        {
            // 電線を持たずに既設の電柱2本を電線ツールで繋ぐ
            // Wire two existing poles with the wire tool while holding no wires
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            SetupInventory(materialCount: 0, wireCount: 0);

            var response = SendConnect(posA, posB, ConnectToolGuid);

            Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
            var connectorA = poleA.GetComponent<IElectricWireConnector>();
            var connectorB = poleB.GetComponent<IElectricWireConnector>();
            Assert.AreEqual(0, connectorA.WireConnections[connectorB.BlockInstanceId].Cost.TotalCount);
        }
    }
}
