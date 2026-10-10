using System.Linq;
using System.Text.RegularExpressions;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Sync.BeltSyncTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 差分の溜め・取り出し・破棄と、再生できない差分を複製が食い違いとして報告すること
    // Recording, taking and discarding diffs, and the replica reporting an unreplayable diff as a divergence
    public class BeltTransportDiffRecorderTest
    {
        [Test]
        public void TakeTickDiffIsEmptyWhenNothingRecorded()
        {
            var recorder = new BeltTransportDiffRecorder();
            var diff = recorder.TakeTickDiff();
            Assert.IsTrue(diff.IsEmpty);
            Assert.AreSame(BeltTickDiff.Empty, diff);
        }

        [Test]
        public void TakeTickDiffReturnsRecordsInOrderAndClears()
        {
            var recorder = new BeltTransportDiffRecorder();
            var first = NewItem(ItemA, BeltEntryDirection.FromBack);
            var second = NewItem(ItemB, BeltEntryDirection.FromLeft);
            recorder.RecordInsert(3, BeltDirection.Back, first);
            recorder.RecordExtract(5, BeltDirection.Front);
            recorder.RecordInsert(1, BeltDirection.Left, second);
            recorder.RecordExtract(2, BeltDirection.Right);

            // 搬入・搬出それぞれ記録した順に、項目を保ったまま出てくる
            // Inserts and extracts each come out in recorded order with every field intact
            var diff = recorder.TakeTickDiff();
            Assert.AreEqual(2, diff.Inserts.Length);
            Assert.AreEqual(3, diff.Inserts[0].SegmentIndex);
            Assert.AreEqual(BeltDirection.Back, diff.Inserts[0].InputDirection);
            AssertSameItem(first, diff.Inserts[0].ToItem());
            Assert.AreEqual(1, diff.Inserts[1].SegmentIndex);
            Assert.AreEqual(BeltDirection.Left, diff.Inserts[1].InputDirection);
            AssertSameItem(second, diff.Inserts[1].ToItem());
            Assert.AreEqual(2, diff.Extracts.Length);
            Assert.AreEqual((5, BeltDirection.Front), (diff.Extracts[0].SegmentIndex, diff.Extracts[0].OutputDirection));
            Assert.AreEqual((2, BeltDirection.Right), (diff.Extracts[1].SegmentIndex, diff.Extracts[1].OutputDirection));

            // 取り出した後は空で、取り出した差分は後の記録に影響されない
            // Empty after taking, and the taken diff is unaffected by later records
            Assert.IsTrue(recorder.TakeTickDiff().IsEmpty);
            recorder.RecordExtract(9, BeltDirection.Front);
            Assert.AreEqual(2, diff.Extracts.Length);
            Assert.AreEqual(1, recorder.TakeTickDiff().Extracts.Length);
        }

        [Test]
        public void DiscardDropsRecords()
        {
            var recorder = new BeltTransportDiffRecorder();
            recorder.RecordInsert(0, BeltDirection.Back, NewItem(ItemA, BeltEntryDirection.FromBack));
            recorder.RecordExtract(0, BeltDirection.Front);
            recorder.Discard();
            Assert.IsTrue(recorder.TakeTickDiff().IsEmpty);
        }

        [Test]
        public void InsertIntoFullSegmentReportsDivergence()
        {
            // 1マスのsegmentは出口の1個で満杯。そこへの搬入は載らない
            // A one-cell segment is full with one item at its exit; an insert into it cannot be placed
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var assembly = Assemble(world);
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0) });
            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(assembly));

            var insert = new BeltMachineInsertRecord(0, BeltDirection.Back, NewItem(ItemB, BeltEntryDirection.FromBack));
            LogAssert.Expect(LogType.Error, new Regex("diverged"));
            Assert.IsFalse(replica.Tick(new BeltTickDiff(new[] { insert }, new BeltMachineExtractRecord[0])));
            Assert.AreEqual(1, ItemCount(replica.CaptureFullState()), "the unplaceable item was not added");
        }

        [Test]
        public void AnnouncedExtractWithoutHandoffReportsDivergence()
        {
            // 機械へ出る直線が空なのに搬出が予告されると、予告は消費されずに残る
            // An extract announced on an empty line into a machine is never consumed
            var world = NewWorld();
            InstallMachinePorts(new Vector3Int[0], new[] { Vector3Int.back });
            PlaceLine(world, 0, 0, 3);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(Assemble(world)));
            var output = replica.Shapes[0].Outputs.Single(o => o.IsMachine);

            var extract = new BeltMachineExtractRecord(0, output.Direction);
            LogAssert.Expect(LogType.Error, new Regex("diverged"));
            Assert.IsFalse(replica.Tick(new BeltTickDiff(new BeltMachineInsertRecord[0], new[] { extract })));

            // 未消費の予告は持ち越さない。次のtickに出口へ来たアイテムを勝手に受け入れない
            // The unconsumed announcement is not carried over, so a later item reaching the exit is not silently accepted
            Assert.IsFalse(replica.MachineReceiverOf(0, output.Direction).TryReceive(output.Direction, 1, NewItem(ItemA, BeltEntryDirection.FromBack)));
        }
    }
}
