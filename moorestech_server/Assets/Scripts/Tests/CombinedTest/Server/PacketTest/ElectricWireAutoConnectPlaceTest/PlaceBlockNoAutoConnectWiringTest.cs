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
            // 機械範囲の電柱を記録どおりのみで設置
            // Place a pole in machine range with NoAutoConnect
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
            // 電線不足でも記録どおりのみは設置される
            // NoAutoConnect places even when auto-connect would reject wire shortage
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
