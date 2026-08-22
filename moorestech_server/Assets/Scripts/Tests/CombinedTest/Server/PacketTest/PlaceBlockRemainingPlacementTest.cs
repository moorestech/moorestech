using System;
using Game.Construction;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// N=3の歯車ベルトで財布課金を検証
    /// Verifies wallet charging (ADR 0026) with the gear belt whose placementsPerCost is 3
    /// </summary>
    public class PlaceBlockRemainingPlacementTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 一本ずつ3回置いても建設コストは1セットしか消費されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 2);
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(2, lookup.GetRemainingCount(PlayerId, belt));

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (11, 0)), new PacketResponseContext(null));
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (12, 0)), new PacketResponseContext(null));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, belt));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(12, 0)));
        }

        [Test]
        public void 残り0で素材もなければ設置されず財布も変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0)));
            Assert.AreEqual(0, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 上り下りは直線と同じ財布を使う()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var straight = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, straight);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            packet.GetPacketResponse(CreatePlaceBlockPayload(straight, (10, 0)), new PacketResponseContext(null));
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.TestGearBeltConveyorUp, (11, 0)), new PacketResponseContext(null));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(11, 0)));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            // 生のBlockIdで引いても財布キーへ正規化されるので上りは直線と同じ残数を返す
            // A raw BlockId is normalized to the wallet key, so the slope reads the same remainder as the straight block
            Assert.AreEqual(1, lookup.GetRemainingCount(PlayerId, straight));
            Assert.AreEqual(1, lookup.GetRemainingCount(PlayerId, ForUnitTestModBlockId.TestGearBeltConveyorUp));
        }

        [Test]
        public void ドラッグ5本は1セットと残り1の消費でセット2つ分になる()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0), (11, 0), (12, 0), (13, 0), (14, 0)), new PacketResponseContext(null));

            // 5本=1セット+2本目開始→2セット消費・残1
            // Five cells = one full set (3) + the start of a second set → two sets consumed, one remaining
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(14, 0)));
        }
    }
}
