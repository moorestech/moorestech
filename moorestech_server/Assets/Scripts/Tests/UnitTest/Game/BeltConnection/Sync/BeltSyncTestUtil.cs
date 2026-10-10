using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量・複製テスト用のワールドと、全量→複製→全量の往復を確かめる補助
    // Worlds for the full-state and replica tests, plus helpers that check the full state -> replica -> full state round trip
    internal static class BeltSyncTestUtil
    {
        // 全量を切り出し、サーバーの組を写しているか確かめてから複製を組み、複製から切り出した全量が項目・ハッシュとも一致するか確かめる
        // Capture the full state, check it mirrors the server assembly, assemble a replica, and check the replica's capture matches field by field and by hash
        internal static BeltTransportReplica AssertRoundTrip(BeltTransportAssembly assembly)
        {
            var full = BeltTransportFullStateCapture.Capture(assembly);
            BeltFullStateAssert.MirrorsAssembly(full, assembly);

            var replica = BeltTransportReplicaAssembler.Assemble(full);
            BeltFullStateAssert.SameSegments(assembly.Segments, replica.Segments);
            var again = replica.CaptureFullState();
            BeltFullStateAssert.AreEqual(full, again);
            Assert.AreEqual(BeltTransportStateHash.Compute(full), BeltTransportStateHash.Compute(again), "hash after round trip");
            return replica;
        }

        internal static uint HashOf(BeltTransportAssembly assembly)
        {
            return BeltTransportStateHash.Compute(BeltTransportFullStateCapture.Capture(assembly));
        }

        internal static uint HashOf(BeltTransportReplica replica)
        {
            return BeltTransportStateHash.Compute(replica.CaptureFullState());
        }

        // 走行中とbuffer内を合わせた、全量に載っているアイテムの総数
        // Total number of items in the full state, running and buffered together
        internal static int ItemCount(BeltTransportFullState full)
        {
            var count = 0;
            foreach (var segment in full.Segments) count += segment.Items.Length + (segment.HasBufferItem ? 1 : 0);
            return count;
        }

        internal static BeltItemState At(ItemId itemId, BeltEntryDirection entryDirection, int distanceToExit)
        {
            return new BeltItemState(new BeltItem(itemId, ItemInstanceId.Create(), entryDirection), distanceToExit);
        }

        // x列に北向きのベルコンを z=fromZ から count マス並べる
        // Place count north-facing belts at column x starting at z=fromZ
        internal static void PlaceLine(IWorldBlockDatastore world, int x, int fromZ, int count)
        {
            for (var z = fromZ; z < fromZ + count; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, z), BlockDirection.North);
        }

        // 列xで、後ろ(z=-1)のベルコンから北向き分配器(z=0)へ入り、前(z=1)・左(x-1)・右(x+1)の1マスのベルコンへ分かれる
        // At column x, the belt behind (z=-1) feeds a north-facing splitter (z=0) that splits into one-cell belts in front (z=1), left (x-1) and right (x+1)
        internal static void PlaceBranch(IWorldBlockDatastore world, int x)
        {
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, -1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, new Vector3Int(x, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x - 1, 0, 0), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x + 1, 0, 0), BlockDirection.East);
        }

        // 列xで、A(z=-1..0)とB(x+1から西向き)が合流M(z=1)へ入り、C(z=2..3)へ流れる
        // At column x, A (z=-1..0) and B (westward from x+1) enter merge M (z=1), which flows into C (z=2..3)
        internal static void PlaceBeltMerge(IWorldBlockDatastore world, int x)
        {
            PlaceLine(world, x, -1, 5);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x + 1, 0, 1), BlockDirection.West);
        }

        // 列xで、平地(z=0)→上り坂(z=1)→1段上の平地(z=2..3)
        // At column x: flat (z=0) -> up slope (z=1) -> flat one level higher (z=2..3)
        internal static void PlaceSlope(IWorldBlockDatastore world, int x)
        {
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, 0), BlockDirection.North);
            Place(world, BeltTestMaster.Up, new Vector3Int(x, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 1, 2), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 1, 3), BlockDirection.North);
        }
    }
}
