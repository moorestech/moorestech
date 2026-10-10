using System.Linq;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Sync.BeltSyncTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量→複製の組み立て→全量が、項目ごと・ハッシュとも元と一致するか
    // Whether full state -> replica assembly -> full state matches the original field by field and by hash
    public class BeltTransportFullStateRoundTripTest
    {
        [Test]
        public void StraightLineWithItemsAtVariousDistancesRoundTrips()
        {
            var world = NewWorld();
            PlaceLine(world, 0, 0, 4);
            var assembly = Assemble(world);

            // 出口ちょうど・離れた位置・密着した2個・入口付近に置き、数tick進めて出口で詰まらせる
            // Place items at the exit, apart, two touching and near the entrance, then tick a few times so they pack at the exit
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[]
            {
                At(ItemA, BeltEntryDirection.FromBack, 0),
                At(ItemB, BeltEntryDirection.FromBack, 300),
                At(ItemC, BeltEntryDirection.FromLeft, 556),
                At(ItemA, BeltEntryDirection.FromBack, 900)
            });
            Tick(assembly, 3);

            var replica = AssertRoundTrip(assembly);
            Assert.AreEqual(4, ItemCount(replica.CaptureFullState()));
        }

        [Test]
        public void MergeFedByBeltAndMachineRoundTripsWithInternalSegmentBufferAndOrder()
        {
            var world = MachineMergeWorld();
            var machine = world.GetBlock(new Vector3Int(-1, 0, 1));
            var mergeBelt = world.GetBlock(MergeCell);
            var assembly = Assemble(world);

            // ベルト側に2個、機械側に1個入れて流し切ると、Cの出口・合流のbuffer・合流のマスに1個ずつ詰まる
            // Two items on the belt side and one from the machine pack up one each at C's exit, the merge buffer and the merge cell
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0), At(ItemB, BeltEntryDirection.FromBack, 100) });
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemC));
            Tick(assembly, 200);

            // もう1個押し込み、合流が埋まっているので内部segmentに残す
            // Push one more; the merge is full, so it stays in the internal segment
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemA));
            Tick(assembly, 3);

            var merge = (BeltBufferedSegment)SegmentAt(assembly, MergeCell);
            Assert.IsTrue(merge.Buffer.HasItem, "merge buffer holds an item");
            Assert.AreNotEqual(InitialMergeOrder, merge.PriorityOrder, "merge order rotated away from the default");
            Assert.AreEqual(1, assembly.Segments[InternalSegmentIndex(assembly)].Count, "internal segment holds the pushed item");

            var replica = AssertRoundTrip(assembly);
            var full = replica.CaptureFullState();
            Assert.IsTrue(full.Segments.Any(s => s.Shape.IsInternal && s.Items.Length == 1), "internal segment survives the round trip with its item");
        }

        [Test]
        public void BranchWithBufferedItemAndRotatedOrderRoundTrips()
        {
            var world = NewWorld();
            PlaceBranch(world, 0);
            var assembly = Assemble(world);

            // 前・右の出口を埋めて左だけ空ける。1個目は左へ出て優先順が回り、2個目はbufferに残る
            // Fill the front and right outputs and leave the left free; the first item leaves left rotating the order, the second stays in the buffer
            SegmentAt(assembly, new Vector3Int(0, 0, 1)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromBack, 0) });
            SegmentAt(assembly, new Vector3Int(1, 0, 0)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromLeft, 0) });
            var branch = (BeltBufferedSegment)SegmentAt(assembly, Vector3Int.zero);
            Assert.AreEqual(BeltSegmentKind.Branch, branch.Kind);
            var feed = SegmentAt(assembly, new Vector3Int(0, 0, -1));
            if (ReferenceEquals(feed, branch))
            {
                branch.RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0), At(ItemB, BeltEntryDirection.FromBack, 256) });
            }
            else
            {
                branch.RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0) });
                feed.RestoreItems(new[] { At(ItemB, BeltEntryDirection.FromBack, 0) });
            }
            Tick(assembly, 200);

            Assert.IsTrue(branch.Buffer.HasItem, "branch buffer holds an item");
            Assert.AreNotEqual(BeltPriority.Create(BeltDirection.Front), branch.PriorityOrder, "branch order rotated away from the default");
            var replica = AssertRoundTrip(assembly);
            Assert.AreEqual(4, ItemCount(replica.CaptureFullState()));
        }

        [Test]
        public void SlopeAndSeveralNetworksInOneWorldRoundTrip()
        {
            var world = NewWorld();
            PlaceSlope(world, 0);
            PlaceBeltMerge(world, 10);
            PlaceBranch(world, 20);
            var assembly = Assemble(world);

            // 坂・合流・分岐の各所へ置き、少し進めてから往復させる
            // Put items on the slope, the merge and the branch, tick a little, then round trip
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 10), At(ItemB, BeltEntryDirection.FromBack, 400) });
            SegmentAt(assembly, new Vector3Int(10, 0, 0)).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 5) });
            SegmentAt(assembly, new Vector3Int(11, 0, 1)).RestoreItems(new[] { At(ItemB, BeltEntryDirection.FromBack, 5) });
            SegmentAt(assembly, new Vector3Int(20, 0, -1)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromBack, 0) });
            Tick(assembly, 7);

            Assert.IsTrue(assembly.Layouts.Any(l => l.Cells.Any(c => c.Position.y == 1)), "slope world reaches the upper level");
            var replica = AssertRoundTrip(assembly);
            Assert.AreEqual(5, ItemCount(replica.CaptureFullState()));
        }

        [Test]
        public void EmptyWorldRoundTrips()
        {
            var assembly = Assemble(NewWorld());
            var replica = AssertRoundTrip(assembly);
            Assert.AreEqual(0, replica.Segments.Length);
        }
    }
}
