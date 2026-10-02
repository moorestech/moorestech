using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport
{
    // 搬出不成立（sent:false）での前進。期待値は手計算
    // Advance without output (sent:false). Expected values are hand-computed
    public class BeltConveyorSegmentAdvanceTest
    {
        [Test]
        public void 先頭が出口に届かなければ全体が速度分進む()
        {
            var segment = Build(8, 300, 0, 50);
            AssertDistances(segment, 300, 556, 862);

            Advance(segment, 100, false);
            AssertDistances(segment, 200, 456, 762);
            Advance(segment, 128, false);
            AssertDistances(segment, 72, 328, 634);
        }

        [Test]
        public void 先頭は出口で止まり以後動かない()
        {
            var segment = Build(2, 50);
            Advance(segment, 128, false);
            AssertDistances(segment, 0);
            Advance(segment, 128, false);
            AssertDistances(segment, 0);
        }

        [Test]
        public void 残り進行量が隙間の途中で尽きると部分的に詰まる()
        {
            // 先頭は10進んで停止、後続は100進み隙間200が110になる
            // Head moves 10 and stops; the follower moves 100 and the gap 200 becomes 110
            var segment = Build(4, 10, 200);
            Advance(segment, 100, false);
            AssertDistances(segment, 0, 366);
            Assert.AreEqual(1, GetBlockSizeAt(segment, 0));
        }

        [Test]
        public void 一度のtickで複数ブロックが先頭ブロックへ合体する()
        {
            // a=20, [b c] 30空き, d 40空き, e 100空き。速度128で a停止→残108→bc合体→残78→d合体→残38→eの隙間62
            // a=20, [b c] 30 apart, d 40 apart, e 100 apart. Speed 128: a stops→108 left→bc merge→78 left→d merges→38 left→e gap 62
            var segment = Build(8, 20, 30, 0, 40, 100);
            AssertDistances(segment, 20, 306, 562, 858, 1214);

            Advance(segment, 128, false);
            AssertDistances(segment, 0, W, 2 * W, 3 * W, 3 * W + W + 62);
            Assert.AreEqual(4, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(4, GetBlockSizeAt(segment, 3));

            // 次のtickでeも合体し5個の1ブロックになる
            // Next tick e merges as well into a single block of five
            Advance(segment, 128, false);
            AssertDistances(segment, 0, W, 2 * W, 3 * W, 4 * W);
            Assert.AreEqual(5, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(5, GetBlockSizeAt(segment, 4));
        }

        [Test]
        public void 隙間と残り進行量が等しければちょうど合体する()
        {
            var segment = Build(4, 0, 128);
            Advance(segment, 128, false);
            AssertDistances(segment, 0, W);
            Assert.AreEqual(2, GetBlockSizeAt(segment, 0));
        }

        [Test]
        public void 出口で密着した走行列は動かない()
        {
            var segment = Build(4, 0, 0, 0);
            Advance(segment, 128, false);
            AssertDistances(segment, 0, W, 2 * W);
            Assert.AreEqual(3, GetBlockSizeAt(segment, 0));
        }

        [Test]
        public void 速度0では動かない()
        {
            var moving = Build(4, 40, 10);
            Advance(moving, 0, false);
            AssertDistances(moving, 40, 306);

            var parked = Build(4, 0, 10);
            Advance(parked, 0, false);
            AssertDistances(parked, 0, 266);
        }

        [Test]
        public void 空の走行列は何もしない()
        {
            var segment = new BeltConveyorSegment(3, 0);
            Advance(segment, 128, false);
            AssertDistances(segment);
        }

        private static BeltConveyorSegment Build(int capacity, params int[] gaps)
        {
            var segment = new BeltConveyorSegment(capacity, 0);
            for (var i = 0; i < gaps.Length; i++) EnqueueTail(segment, gaps[i], MakeItem(i + 1));
            return segment;
        }
    }
}
