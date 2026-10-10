using System.Linq;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;
using NUnit.Framework;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Sync.BeltSyncTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量・1tick差分の通信形が、MessagePackのシリアライズ往復で項目・ハッシュとも変わらないか
    // Whether the wire forms of the full state and a one-tick diff survive a MessagePack serialize round trip field by field and by hash
    public class BeltTransportMessagePackRoundTripTest
    {
        private const uint ServerTick = 123456;
        private const uint TickSequenceId = 7;

        [Test]
        public void FullStateWithMergeInternalSegmentAndBranchRoundTrips()
        {
            var world = MachineMergeWorld();
            PlaceBranch(world, 20);
            var machine = world.GetBlock(new Vector3Int(-1, 0, 1));
            var mergeBelt = world.GetBlock(MergeCell);
            var assembly = Assemble(world);

            // 合流側: ベルトに2個・機械から1個流して詰まらせ、最後の1個を内部segmentに残す
            // Merge side: run two belt items and one machine item until they pack, then leave one more in the internal segment
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0), At(ItemB, BeltEntryDirection.FromBack, 100) });
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemC));

            // 分岐側: 前・右の出口を埋め、左へ1個出したあと2個目をbufferに残す
            // Branch side: fill the front and right outputs so one item leaves left and the second stays in the buffer
            SegmentAt(assembly, new Vector3Int(20, 0, 1)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromBack, 0) });
            SegmentAt(assembly, new Vector3Int(21, 0, 0)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromLeft, 0) });
            SegmentAt(assembly, new Vector3Int(20, 0, -1)).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0), At(ItemB, BeltEntryDirection.FromBack, 256) });
            Tick(assembly, 200);
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemA));
            Tick(assembly, 3);

            var full = BeltTransportFullStateCapture.Capture(assembly);
            Assert.IsTrue(full.Segments.Any(s => s.Shape.Kind == BeltSegmentKind.Normal && s.Items.Length > 0), "normal segment with running items");
            Assert.IsTrue(full.Segments.Any(s => s.Shape.Kind == BeltSegmentKind.Merge && s.HasBufferItem), "merge with a buffered item");
            Assert.IsTrue(full.Segments.Any(s => s.Shape.Kind == BeltSegmentKind.Branch && s.HasBufferItem), "branch with a buffered item");
            Assert.IsTrue(full.Segments.Any(s => s.Shape.IsInternal && s.Items.Length == 1), "internal segment with an item");

            var message = RoundTrip(new BeltTransportFullStateMessagePack(ServerTick, TickSequenceId, full));
            Assert.AreEqual(ServerTick, message.ServerTick);
            Assert.AreEqual(TickSequenceId, message.TickSequenceId);
            var decoded = message.ToFullState();
            BeltFullStateAssert.AreEqual(full, decoded);
            Assert.AreEqual(BeltTransportStateHash.Compute(full), BeltTransportStateHash.Compute(decoded), "hash after the wire round trip");
        }

        [Test]
        public void TickDiffRoundTripsFieldByField()
        {
            var inserts = new[]
            {
                new BeltMachineInsertRecord(3, BeltDirection.Left, new BeltItem(ItemA, ItemInstanceId.Create(), BeltEntryDirection.FromLeft)),
                new BeltMachineInsertRecord(0, BeltDirection.Back, new BeltItem(ItemC, ItemInstanceId.Create(), BeltEntryDirection.FromBackAbove))
            };
            var extracts = new[] { new BeltMachineExtractRecord(5, BeltDirection.Front), new BeltMachineExtractRecord(2, BeltDirection.Right) };
            Assert.AreNotEqual(0L, inserts[0].ItemInstanceId.AsPrimitive(), "instance ids are non-zero");

            var message = RoundTrip(new BeltTransportTickDiffMessagePack(ServerTick, TickSequenceId, new BeltTickDiff(inserts, extracts)));
            Assert.AreEqual(ServerTick, message.ServerTick);
            Assert.AreEqual(TickSequenceId, message.TickSequenceId);
            var decoded = message.ToDiff();

            Assert.AreEqual(inserts.Length, decoded.Inserts.Length, "insert count");
            for (var i = 0; i < inserts.Length; i++)
            {
                Assert.AreEqual(inserts[i].SegmentIndex, decoded.Inserts[i].SegmentIndex, $"insert {i} segment");
                Assert.AreEqual(inserts[i].InputDirection, decoded.Inserts[i].InputDirection, $"insert {i} input direction");
                Assert.AreEqual(inserts[i].ItemId, decoded.Inserts[i].ItemId, $"insert {i} item id");
                Assert.AreEqual(inserts[i].ItemInstanceId, decoded.Inserts[i].ItemInstanceId, $"insert {i} instance id");
                Assert.AreEqual(inserts[i].EntryDirection, decoded.Inserts[i].EntryDirection, $"insert {i} entry direction");
            }
            Assert.AreEqual(extracts.Length, decoded.Extracts.Length, "extract count");
            for (var i = 0; i < extracts.Length; i++)
            {
                Assert.AreEqual(extracts[i].SegmentIndex, decoded.Extracts[i].SegmentIndex, $"extract {i} segment");
                Assert.AreEqual(extracts[i].OutputDirection, decoded.Extracts[i].OutputDirection, $"extract {i} output direction");
            }
        }

        [Test]
        public void EmptyTickDiffRoundTripsToEmpty()
        {
            var message = RoundTrip(new BeltTransportTickDiffMessagePack(ServerTick, TickSequenceId, BeltTickDiff.Empty));
            Assert.IsTrue(message.ToDiff().IsEmpty);
            Assert.AreEqual(ServerTick, message.ServerTick);
            Assert.AreEqual(TickSequenceId, message.TickSequenceId);
        }

        private static T RoundTrip<T>(T message)
        {
            return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(message));
        }
    }
}
