using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport
{
    // 先頭の取り除きと搬出成立（sent:true）での前進。期待値は手計算
    // Head removal and advance with successful output (sent:true). Expected values are hand-computed
    public class BeltConveyorSegmentDequeueTest
    {
        [Test]
        public void 搬出成立で先頭が消え残りは速度分進む()
        {
            var segment = Build(4, 50, 0, 20);
            AssertDistances(segment, 50, 306, 582);
            Assert.AreEqual(78, GetOutputLength(segment, 128));

            Advance(segment, 128, true);
            AssertDistances(segment, 178, 454);
            Assert.AreEqual(2, segment.CaptureItems()[0].Item.ItemInstanceId.AsPrimitive());

            // 最後の1個を搬出すると空になる
            // Sending the last remaining item leaves the segment empty
            var single = Build(2, 0);
            Advance(single, 100, true);
            AssertDistances(single);
            Assert.AreEqual(0, GetTotalLength(single));
        }

        [Test]
        public void 取り除きは残りの位置を変えない()
        {
            var segment = Build(4, 30, 70);
            DequeueHead(segment);
            AssertDistances(segment, 30 + W + 70);
            Assert.AreEqual(2, segment.CaptureItems()[0].Item.ItemInstanceId.AsPrimitive());
        }

        [Test]
        public void 密着ブロックの途中から取り除いても後の合体が正しい()
        {
            // [a b c] 100空き [d e]。aを取り除くと [b c] [d e]
            // [a b c] 100 apart [d e]. Removing a leaves [b c] [d e]
            var segment = Build(6, 0, 0, 0, 100, 0);
            DequeueHead(segment);
            AssertDistances(segment, W, 2 * W, 3 * W + 100, 4 * W + 100);
            Assert.AreEqual(2, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(2, GetBlockSizeAt(segment, 1));

            // 2tickで先頭が出口へ着き、3tick目に後続ブロックが合体する
            // The head reaches the exit in two ticks; the following block merges on the third
            Advance(segment, 128, false);
            AssertDistances(segment, 128, 384, 740, 996);
            Advance(segment, 128, false);
            AssertDistances(segment, 0, W, 612, 868);
            Advance(segment, 128, false);
            AssertDistances(segment, 0, W, 2 * W, 3 * W);
            Assert.AreEqual(4, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(4, GetBlockSizeAt(segment, 3));
        }

        [Test]
        public void リングの折り返しをまたぐ密着ブロックを保つ()
        {
            var segment = Build(3, 0, 0, 0);
            DequeueHead(segment);
            AssertDistances(segment, W, 2 * W);

            // 物理位置0へ折り返して追加し、物理1,2,0にまたがる3個ブロックになる
            // Append wrapping to physical slot 0, forming a block of three over slots 1,2,0
            EnqueueTail(segment, 0, MakeItem(4));
            AssertDistances(segment, W, 2 * W, 3 * W);
            Assert.AreEqual(3, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(3, GetBlockSizeAt(segment, 2));

            DequeueHead(segment);
            DequeueHead(segment);
            AssertDistances(segment, 3 * W);
            Assert.AreEqual(4, segment.CaptureItems()[0].Item.ItemInstanceId.AsPrimitive());

            Advance(segment, 128, false);
            AssertDistances(segment, 3 * W - 128);
        }

        private static BeltConveyorSegment Build(int capacity, params int[] gaps)
        {
            var segment = CreateNormal(capacity, 0);
            for (var i = 0; i < gaps.Length; i++) EnqueueTail(segment, gaps[i], MakeItem(i + 1));
            return segment;
        }
    }
}
