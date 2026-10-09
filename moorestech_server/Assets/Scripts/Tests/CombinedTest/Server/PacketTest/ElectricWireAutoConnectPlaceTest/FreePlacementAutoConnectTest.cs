using System.IO;
using Common.Debug;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class FreePlacementAutoConnectTest : ElectricWireAutoConnectPlaceTestBase
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
        public void 無料設置でも電線アイテム無しで自動配線されコスト0で記録される()
        {
            // 電線アイテム0・建設コスト未所持のまま、電柱2本と機械を順に置く
            // Place two poles and a machine with zero wires and no construction materials
            var (packet, serviceProvider) = CreateServer();
            var inventory = SetupWire(serviceProvider, 0);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(3, 0, 0));
            PlaceBlock(packet, ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 1));

            var poleA = GetConnector(new Vector3Int(0, 0, 0));
            var poleB = GetConnector(new Vector3Int(3, 0, 0));
            var machine = GetConnector(new Vector3Int(1, 0, 1));

            // 電柱同士と機械が通常設置と同じく自動配線される
            // Poles and the machine are auto-wired just like normal placement
            Assert.IsTrue(poleA.ContainsWireConnection(poleB.BlockInstanceId));
            Assert.AreEqual(1, machine.WireConnections.Count);

            // 支払いが無いので消費も記録も0、撤去時の返却も0
            // Nothing was paid, so nothing is consumed, recorded or refunded
            Assert.AreEqual(0, GetWireCount(inventory));
            foreach (var connection in poleA.WireConnections.Values) Assert.AreEqual(0, connection.Cost.TotalCount);
            foreach (var connection in machine.WireConnections.Values) Assert.AreEqual(0, connection.Cost.TotalCount);
            Assert.AreEqual(0, ((IGetRefundItemsInfo)poleA).GetRefundItems().Count);
        }

        [Test]
        public void 無料設置では未解放ブロックも置ける()
        {
            // 解放もコストも用意せずに未解放の電柱を置く
            // Place a locked pole without unlocking it or holding its cost
            var (packet, _) = CreateServer();
            PlaceBlock(packet, ForUnitTestModBlockId.LockedElectricPoleId, new Vector3Int(0, 0, 0));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(0, 0, 0)));
        }

        private static IElectricWireConnector GetConnector(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.GetBlock(position).GetComponent<IElectricWireConnector>();
        }
    }
}
