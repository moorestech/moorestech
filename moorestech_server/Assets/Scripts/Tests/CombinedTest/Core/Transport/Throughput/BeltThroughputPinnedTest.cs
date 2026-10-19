using Core.BeltTransport;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // B: 100% にならないのが仕様どおりの配置。今の実装が出す正確な値をピン止めし、黙って変わらないようにする(ADR 0084)
    // B: layouts that are not 100% by design; the exact value the current implementation yields is pinned so it cannot change silently (ADR 0084)
    // 根拠: README §1tickの順序 4.「空きを全件記録してから前進する」。通常segment同士の境界では、下流の先頭がこのtickに出ていく分の空きを次のtickまで使えない
    // Basis: README §1tick order 4, "capture every offer, then advance": at a normal-to-normal boundary the space the downstream head frees this tick is unusable until the next
    // 値を変える PR は理由を ADR に書く(ユーザー裁定 2026-10-18「1 正確な個数をピン止め」)
    // A PR that changes a value records the reason in an ADR (user ruling 2026-10-18 "pin the exact count")
    [Category("CiShardServerBeltThroughput")]
    public class BeltThroughputPinnedTest
    {
        private const int WoodPeriod = 20;
        private const int WoodSpeed = 18;
        private static readonly Vector3Int RingCell = new(0, 0, 0);
        private static readonly Vector3Int RingFeeder = new(1, 0, -1);

        // B1: 木(18)→鉄(40)→木(18)、各10マス。加速側・減速側の両方の速度境界を含む。木・鉄・木の各 segment の搬出数もピン止め
        // B1: wood (18) -> iron (40) -> wood (18), ten cells each; includes both speed boundaries. The exits of each segment are pinned too
        [Test]
        public void B1_WoodIronWoodSpeedBoundaries([Values] BeltPlacementOrder order)
        {
            const int pinnedSourceTotal = 141;
            int[] pinnedSegmentExits = { 141, 138, 137 };
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, 10);
            Line(scenario, ForUnitTestModBlockId.IronGearBelt, new Vector3Int(0, 0, 10), BlockDirection.North, 10);
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 20), BlockDirection.North, 10);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 5)).Watch(new Vector3Int(0, 0, 15)).Watch(new Vector3Int(0, 0, 25));
            Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            Assert.AreEqual(pinnedSourceTotal, run.SourceTotal, $"B1 pinned source emissions; sources={string.Join(",", run.SourceCounts)} watched={string.Join(",", run.WatchedExits)}");
            CollectionAssert.AreEqual(pinnedSegmentExits, run.WatchedExits, $"B1 pinned exits of wood/iron/wood; watched={string.Join(",", run.WatchedExits)}");
            Assert.AreEqual(run.SourceTotal - run.SinkTotal, run.OnBeltsDelta, "B1 conservation");
            Assert.Less(run.SourceTotal, BeltThroughputWindow.ExpectedTotal(run.Window, WoodPeriod), "B1 is below 100% by design; if it reaches 100%, move it to A");
        }

        // B4: 自己接続した輪(満杯でない、入出力なし)。個数は保存されるが、継ぎ目を越えるたびに通常segment境界の1tick待ちが掛かるので「毎tick速度分進む」にはならない。400tick後の位置をピン止め
        // B4: a self-linked ring (not full, no input or output); items are conserved, but each seam crossing pays the normal-boundary one-tick wait, so "advance by the speed every tick" does not hold. Positions after 400 ticks are pinned
        [Test]
        public void B4_RingNotFullKeepsCirculating([Values] BeltPlacementOrder order)
        {
            const int seedTicks = 100;
            const int observeTicks = 400;
            int[] pinnedBefore = { 2124, 2484, 2844, 3204, 3582 };
            int[] pinnedAfter = { 0, 378, 756, 3564, 3942 };
            var session = SeededRing(order, seedTicks);
            var before = BeltWorldTestUtil.SegmentAt(RingCell).CaptureItems();
            CollectionAssert.AreEqual(pinnedBefore, System.Array.ConvertAll(before, s => s.DistanceToExit), $"B4 seeded positions; before={Describe(before)}");
            Assert.Less(before.Length, BeltWorldTestUtil.SegmentAt(RingCell).Capacity, "B4 the ring must not be full");

            session.TickMany(observeTicks);
            var after = BeltWorldTestUtil.SegmentAt(RingCell).CaptureItems();
            Assert.AreEqual(before.Length, after.Length, "B4 items are conserved on the ring");
            CollectionAssert.AreEqual(pinnedAfter, System.Array.ConvertAll(after, s => s.DistanceToExit), $"B4 pinned positions after {observeTicks} ticks (seam waits included); after={Describe(after)}");

            // 継ぎ目の待ちが無ければ 7200 進んでいるはず。待ちが消えて一致したら A へ移す
            // Without seam waits every item would have advanced 7200; if that ever matches, move this to A
            var length = BeltWorldTestUtil.SegmentAt(RingCell).Capacity * BeltConstants.ItemWidth;
            var lossFree = System.Array.ConvertAll(before, s => ((s.DistanceToExit - observeTicks * WoodSpeed) % length + length) % length);
            System.Array.Sort(lossFree);
            CollectionAssert.AreNotEqual(lossFree, pinnedAfter, "B4 is below the loss-free motion by design");
        }

        // B5: 満杯の輪は動かない
        // B5: a full ring does not move
        [Test]
        public void B5_FullRingStops([Values] BeltPlacementOrder order)
        {
            const int fillTicks = 2000;
            var session = SeededRing(order, fillTicks);
            var before = BeltWorldTestUtil.SegmentAt(RingCell).CaptureItems();
            Assert.AreEqual(BeltWorldTestUtil.SegmentAt(RingCell).Capacity, before.Length, $"B5 the ring holds one item per cell when full; before={Describe(before)}");
            session.TickMany(100);
            var after = BeltWorldTestUtil.SegmentAt(RingCell).CaptureItems();
            Assert.AreEqual(before.Length, after.Length, "B5 items are conserved");
            for (var i = 0; i < before.Length; i++) Assert.AreEqual(before[i].DistanceToExit, after[i].DistanceToExit, $"B5 item {i} did not move; before={Describe(before)} after={Describe(after)}");
        }

        // 4×4 の輪(木の歯車ベルト、反時計回り)に供給チェストを横から付けて seedTicks 流し込み、チェストを撤去して1本の自己接続segmentにする
        // Feed a 4×4 counter-clockwise ring of wood gear belts from a side chest for seedTicks, then remove the chest so the ring becomes one self-linked segment
        private static BeltThroughputSession SeededRing(BeltPlacementOrder order, int seedTicks)
        {
            var scenario = new BeltThroughputScenario().Source(RingFeeder);
            var belt = ForUnitTestModBlockId.WoodGearBelt;
            Line(scenario, belt, new Vector3Int(0, 0, 0), BlockDirection.East, 3);
            Line(scenario, belt, new Vector3Int(3, 0, 0), BlockDirection.North, 3);
            Line(scenario, belt, new Vector3Int(3, 0, 3), BlockDirection.West, 3);
            Line(scenario, belt, new Vector3Int(0, 0, 3), BlockDirection.South, 3);
            Generator(scenario, new Vector3Int(1, 0, 1));
            var session = scenario.Start(order);
            session.TickMany(seedTicks);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.RemoveBlock(RingFeeder, BlockRemoveReason.ManualRemove), "removed the feeder chest");

            // 撤去で組が汚れ、次のtick先頭で輪が1本の自己接続segmentに組み直される
            // The removal dirties the assembly; at the next tick head the ring is rebuilt as one self-linked segment
            session.TickOnce();
            Assert.AreEqual(1, BeltWorldTestUtil.Assembly().Segments.Length, "the ring is one segment after the feeder is gone");
            return session;
        }

        private static string Describe(BeltItemState[] states)
        {
            return string.Join("/", System.Array.ConvertAll(states, s => s.DistanceToExit.ToString()));
        }
    }
}
