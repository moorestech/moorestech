using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Sync;
using Tests.UnitTest.Game.BeltConnection.Transport;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.BeltDiffReplayTestUtil;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 実tick経路で機械が押し込み・受け入れるワールドを、全量1回＋毎tickの差分だけで複製が再現し続けるか
    // Whether a replica fed only one full state plus per-tick diffs keeps reproducing a world where machines push and accept through the real tick path
    public class BeltTransportDiffReplayTest
    {
        private const int TickCount = 400;
        private static readonly ItemId ItemA = new(1);
        private static readonly ItemId ItemB = new(2);
        private static readonly ItemId ItemC = new(3);

        [Test]
        public void MachineFedNetworkReplaysIdenticallyFromDiffs()
        {
            CreateServer();
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ItemA);

            // (a)(d) 機械→3マスの直線→あと4個で満杯になるチェスト。途中から拒否して搬出が止まる
            // (a)(d) machine -> three-cell line -> a chest that is full after four more; it starts refusing mid-run and handoffs stop
            var sourceLine = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z <= 2; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            var filling = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            for (var i = 0; i < filling.GetSlotSize() - 1; i++) filling.SetItem(i, ServerContext.ItemStackFactory.Create(ItemB, 1));
            filling.SetItem(filling.GetSlotSize() - 1, ServerContext.ItemStackFactory.Create(ItemA, maxStack - 4));

            // (b) 直線へベルコン(z=1)と機械(z=2の左、内部segment)が合流し、チェストで終わる
            // (b) a belt (at z=1) and a machine (left of z=2, internal segment) merge into a line that ends in a chest
            var sourceMain = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(10, 0, -2), BlockDirection.North));
            for (var z = -1; z <= 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(10, 0, z), BlockDirection.North);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(11, 0, 1), BlockDirection.West);
            var sourceSide = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(12, 0, 1), BlockDirection.North));
            var sourceMachineMerge = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(9, 0, 2), BlockDirection.North));
            var sinkMerge = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(10, 0, 4), BlockDirection.North));

            // (c) 分岐の前はベルコン→チェスト、左はチェストへ直接、右は行き止まりのベルコン
            // (c) the branch outputs forward to a belt -> chest, left straight into a chest, right into a dead-end belt
            var sourceBranch = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(20, 0, -2), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(20, 0, -1), BlockDirection.North);
            Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, new Vector3Int(20, 0, 0), BlockDirection.North);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(20, 0, 1), BlockDirection.North);
            var sinkBranchForward = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(20, 0, 2), BlockDirection.North));
            var sinkBranchLeft = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(19, 0, 0), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(21, 0, 0), BlockDirection.East);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(22, 0, 0), BlockDirection.East);

            foreach (var source in new[] { sourceLine, sourceMain, sourceBranch }) source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, maxStack));
            sourceSide.SetItem(0, ServerContext.ItemStackFactory.Create(ItemB, ItemStackLevelDataStore.Instance.GetMaxStack(ItemB)));
            sourceMachineMerge.SetItem(0, ServerContext.ItemStackFactory.Create(ItemC, ItemStackLevelDataStore.Instance.GetMaxStack(ItemC)));

            // 1tick目の先頭で組が作られる。その後の全量から複製を始める
            // The assembly is built at the head of tick 1; the replica starts from the full state after it
            TickServer();
            var replica = StartReplica();
            var assembly = Datastore().Assembly;
            var fillingSegment = BeltTransportTestUtil.SegmentIndexAt(assembly, Vector3Int.zero);
            var internalSegment = BeltTransportTestUtil.InternalSegmentIndex(assembly);
            var branchSegment = BeltTransportTestUtil.SegmentIndexAt(assembly, new Vector3Int(20, 0, 0));
            var branchMachineOutput = replica.Shapes[branchSegment].Outputs.Single(o => o.IsMachine).Direction;

            var inserts = 0;
            var internalInserts = 0;
            var extracts = 0;
            var fillingExtracts = 0;
            var lastFillingExtractTick = 0;
            var branchMachineExtracts = 0;
            for (var tick = 1; tick <= TickCount; tick++)
            {
                var diff = TickServer();
                Assert.AreSame(assembly, Datastore().Assembly, "no rebuild during the run");
                ReplayAndAssert(replica, diff, tick);

                inserts += diff.Inserts.Length;
                internalInserts += diff.Inserts.Count(i => i.SegmentIndex == internalSegment);
                extracts += diff.Extracts.Length;
                branchMachineExtracts += diff.Extracts.Count(e => e.SegmentIndex == branchSegment && e.OutputDirection == branchMachineOutput);
                var fillingNow = diff.Extracts.Count(e => e.SegmentIndex == fillingSegment);
                fillingExtracts += fillingNow;
                if (fillingNow > 0) lastFillingExtractTick = tick;
            }

            // 搬入・搬出・内部segmentへの搬入・bufferから機械への搬出がどれも実際に再生された
            // Inserts, extracts, inserts into the internal segment and buffer-to-machine extracts were all actually replayed
            Assert.Greater(inserts, 0, "machine pushes replayed");
            Assert.Greater(internalInserts, 0, "machine pushes into the internal segment replayed");
            Assert.Greater(extracts, 0, "machine handoffs replayed");
            Assert.Greater(branchMachineExtracts, 0, "buffer-to-machine handoffs replayed");
            Assert.Greater(CountOf(sinkMerge, ItemB), 0, "the merge delivered side-belt items");
            Assert.Greater(CountOf(sinkMerge, ItemC), 0, "the merge delivered machine-merge items");
            Assert.Greater(CountOf(sinkBranchForward, ItemA), 0, "the branch delivered forward");
            Assert.AreEqual(branchMachineExtracts, CountOf(sinkBranchLeft, ItemA), "every left handoff reached the chest");

            // 満杯のチェストは4個だけ受け、それ以降の搬出は差分に現れず、直線は3個で詰まった
            // The filling chest took exactly four, later handoffs never appeared in the diff, and the line jammed with three items
            Assert.AreEqual(maxStack, CountOf(filling, ItemA), "the filling chest is full");
            Assert.AreEqual(4, fillingExtracts, "handoffs into the filling chest");
            Assert.Less(lastFillingExtractTick, TickCount - 50, "handoffs stopped mid-run");
            Assert.AreEqual(3, ItemsOnSegmentAt(Vector3Int.zero).Length, "the line jammed");

            BeltFullStateAssert.AreEqual(BeltTransportFullStateCapture.Capture(Datastore().Assembly), replica.CaptureFullState());
        }

        [Test]
        public void InletPushBetweenTicksIsReplayedOnNextTick()
        {
            CreateServer();
            var machine = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var inlet = Inlet(Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North));
            for (var z = 1; z <= 2; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            var sink = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            TickServer();
            var replica = StartReplica();

            // tickの外(block更新の外)で搬入口へ押し込んだ分も、次のtickの差分に載って同じ順で再生される
            // A push into the inlet outside the tick (outside block updates) lands in the next tick's diff and replays in order
            var pushed = 0;
            for (var tick = 1; tick <= 300; tick++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ItemA, 1);
                var entered = tick % 50 == 0 && PushThroughInlet(inlet, machine, stack);
                var diff = TickServer();
                if (entered)
                {
                    pushed++;
                    Assert.AreEqual(1, diff.Inserts.Length, $"inserts in the diff of tick {tick}");
                    Assert.AreEqual(stack.ItemInstanceId, diff.Inserts[0].ItemInstanceId, $"the push before tick {tick} is in its diff");
                }
                ReplayAndAssert(replica, diff, tick);
            }
            Assert.AreEqual(6, pushed, "every push entered");
            Assert.Greater(CountOf(sink, ItemA), 0, "pushed items reached the chest");
            BeltFullStateAssert.AreEqual(BeltTransportFullStateCapture.Capture(Datastore().Assembly), replica.CaptureFullState());
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
