using System;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceBlockNoAutoConnectWiringTest : ElectricWireAutoConnectPlaceTestBase
    {
        [Test]
        public void 記録どおりのみで設置すると範囲内に機械があっても電線を張らず消費もしない()
        {
            // 電柱の機械範囲内に機械を置き、電線を持たせた状態で電柱を記録どおりのみで設置する
            // Put a machine in the pole's machine range and place the pole with NoAutoConnect while holding wires
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = SetupWire(serviceProvider, 5);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.NoAutoConnect);

            var pole = worldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsNotNull(pole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, machine.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(5, GetWireCount(inventory));
        }

        [Test]
        public void 記録どおりのみなら電線ゼロでも電線不足で拒否されない()
        {
            // 自動接続なら電線不足で拒否される配置でも、記録どおりのみは設置される
            // A layout that auto-connect rejects for wire shortage is still placed under NoAutoConnect
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlockWithWiring(packet, ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero, BlockPlacementWiring.NoAutoConnect);

            Assert.IsTrue(worldBlockDatastore.Exists(Vector3Int.zero));
        }
    }
}
