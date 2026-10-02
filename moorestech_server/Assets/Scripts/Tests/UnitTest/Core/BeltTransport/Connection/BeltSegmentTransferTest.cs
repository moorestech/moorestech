using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Connection.BeltConnectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 通常segment間の段階4。空き記録→全前進→搬入反映を参照実装の順に手で回す。期待値は手計算
    // Stage 4 between normal segments, driven by hand as capture → advance all → apply. Expected values are hand-computed
    public class BeltSegmentTransferTest
    {
        [Test]
        public void 記録した空き以下なら搬送し搬入先の前進後に進入距離を保って置く()
        {
            // 搬入先の空きはW-100=156で、進入距離70が収まる
            // The target's offer is W-100=156, which fits the entry length 70
            var source = CreateNormal(1, 0);
            EnqueueTail(source, 30, MakeItem(1));
            var target = CreateNormal(2, 0);
            EnqueueTail(target, 100, MakeItem(2));
            source.ConnectTo(target, BeltDirection.Front);

            BeginTickAt(source, 100);
            BeginTickAt(target, 50);
            RunStageFour(source, target);
            AssertDistances(source);
            AssertDistances(target, 50, 2 * W - 70);
            CollectionAssert.AreEqual(new long[] { 2, 1 }, Serials(target));
        }

        [Test]
        public void 進入距離が記録した空きを超えると搬送せず出口で止まる()
        {
            // 空きW-160=96に進入距離100は入らない
            // The entry length 100 does not fit the offer W-160=96
            var source = CreateNormal(1, 0);
            EnqueueTail(source, 0, MakeItem(1));
            var target = CreateNormal(2, 0);
            EnqueueTail(target, 160, MakeItem(2));
            source.ConnectTo(target, BeltDirection.Front);

            BeginTickAt(source, 100);
            BeginTickAt(target, 50);
            RunStageFour(source, target);
            AssertDistances(source, 0);
            AssertDistances(target, 110);
        }

        [Test]
        public void 同じtickの更新中に空いた場所は使わず境界で1tick待つ()
        {
            // 搬入先が先に前進して機械へ搬出しても、記録済みの空き0で判定する
            // Even though the target advances first and outputs to a machine, the recorded offer of 0 decides
            var source = CreateNormal(1, 0);
            EnqueueTail(source, 0, MakeItem(1));
            var target = CreateNormal(1, 0);
            EnqueueTail(target, 0, MakeItem(2));
            var machine = new FakeBeltReceiver(W, true);
            source.ConnectTo(target, BeltDirection.Front);
            target.ConnectTo(machine, BeltDirection.Front);

            BeginTickAt(source, 100);
            BeginTickAt(target, 100);
            RunStageFour(target, source);
            Assert.AreEqual(1, machine.ReceivedItems.Count);
            AssertDistances(target);
            AssertDistances(source, 0);

            // 次のtickで空き1マスを記録して搬送する
            // The next tick records a full-cell offer and transfers
            BeginTickAt(source, 100);
            BeginTickAt(target, 100);
            RunStageFour(target, source);
            AssertDistances(source);
            AssertDistances(target, W - 100);
        }

        [Test]
        public void 自分自身へ接続した輪はアイテムが周回する()
        {
            // 4マスの輪に出口0と2W。毎tick100進み、出口を越えた先頭は入口へ回る
            // A four-cell loop with items at 0 and 2W. Each tick moves 100 and the head past the exit wraps to the entrance
            var loop = CreateNormal(4, 0);
            EnqueueTail(loop, 0, MakeItem(1));
            EnqueueTail(loop, W, MakeItem(2));
            loop.ConnectTo(loop, BeltDirection.Front);

            BeginTickAt(loop, 100);
            RunStageFour(loop);
            AssertDistances(loop, 2 * W - 100, 4 * W - 100);
            CollectionAssert.AreEqual(new long[] { 2, 1 }, Serials(loop));

            // 5tick後に2個目が出口を88越えて入口へ回る
            // Five ticks later the second item passes the exit by 88 and wraps around
            for (var tick = 0; tick < 5; tick++)
            {
                BeginTickAt(loop, 100);
                RunStageFour(loop);
            }
            AssertDistances(loop, 4 * W - 600, 4 * W - 88);
            CollectionAssert.AreEqual(new long[] { 1, 2 }, Serials(loop));
        }

        [Test]
        public void 完全に満杯の輪は空きが0で停止する()
        {
            var loop = CreateNormal(2, 0);
            EnqueueTail(loop, 0, MakeItem(1));
            EnqueueTail(loop, 0, MakeItem(2));
            loop.ConnectTo(loop, BeltDirection.Left);

            for (var tick = 0; tick < 3; tick++)
            {
                BeginTickAt(loop, BeltConstants.MaxSpeed);
                RunStageFour(loop);
            }
            AssertDistances(loop, 0, W);
            CollectionAssert.AreEqual(new long[] { 1, 2 }, Serials(loop));
        }

        [Test]
        public void 合流や機械への搬出は遅延しない()
        {
            var toNormal = CreateNormal(1, 0);
            toNormal.ConnectTo(CreateNormal(1, 0), BeltDirection.Front);
            var toMerge = CreateNormal(1, 0);
            var merge = CreateMerge(BeltPriority.InitializeFromDirection, BeltDirection.Front);
            toMerge.ConnectTo(merge, BeltDirection.Front);
            var toBranch = CreateNormal(1, 0);
            toBranch.ConnectTo(CreateBranch(1, BeltPriority.InitializeFromDirection, BeltDirection.Front), BeltDirection.Front);
            var toMachine = CreateNormal(1, 0);
            toMachine.ConnectTo(new FakeBeltReceiver(W, true), BeltDirection.Front);
            var unconnected = CreateNormal(1, 0);

            Assert.IsNotNull(CacheTransfer(toNormal));
            Assert.IsNull(CacheTransfer(toMerge));
            Assert.IsNull(CacheTransfer(toBranch));
            Assert.IsNull(CacheTransfer(toMachine));
            Assert.IsNull(CacheTransfer(unconnected));
            Assert.AreEqual(1, CacheTransfers(new[] { toNormal, toMerge, toBranch, toMachine, unconnected }).Length);

            // 合流へは予約に従い段階4の中で直接搬入する
            // Into a merge, the item enters directly within stage 4 following the reservation
            EnqueueTail(toMerge, 0, MakeItem(1));
            BeginTickAt(toMerge, 100);
            ResolveInput(merge);
            CacheTransfer(toMerge);
            AdvanceAndTransfer(toMerge);
            AssertDistances(merge, W - 100);
            AssertDistances(toMerge);
        }

        // 段階4を参照実装の順に実行する。各段階は全件完了してから次へ進む
        // Run stage 4 in the reference order, finishing every item of a phase before the next
        private static void RunStageFour(params BeltConveyorSegment[] normal)
        {
            var transfers = CacheTransfers(normal);
            foreach (var transfer in transfers) CaptureOffer(transfer);
            foreach (var segment in normal) AdvanceAndTransfer(segment);
            foreach (var transfer in transfers) ApplyTransfer(transfer);
        }

        private static long[] Serials(BeltConveyorSegment segment)
        {
            var states = segment.CaptureItems();
            var serials = new long[states.Length];
            for (var i = 0; i < states.Length; i++) serials[i] = states[i].Item.ItemInstanceId.AsPrimitive();
            return serials;
        }
    }
}
