using System;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltConveyorSegmentTest
    {
        [Test]
        public void 隙間付きで追加すると出口までの距離と占有長が積み上がる()
        {
            var segment = CreateNormal(4, 0);
            Assert.AreEqual(4 * W, GetLength(segment));

            // 出口から100、密着、30空けて、密着の順に追加する
            // Append at 100 from the exit, packed, 30 apart, then packed
            EnqueueTail(segment, 100, MakeItem(1));
            AssertDistances(segment, 100);
            Assert.AreEqual(100 + W, GetTotalLength(segment));

            EnqueueTail(segment, 0, MakeItem(2));
            EnqueueTail(segment, 30, MakeItem(3));
            EnqueueTail(segment, 0, MakeItem(4));
            AssertDistances(segment, 100, 100 + W, 130 + 2 * W, 130 + 3 * W);
            Assert.AreEqual(130 + 4 * W, GetTotalLength(segment));
            Assert.AreEqual(4, segment.Count);

            // 先頭ブロック2個・後続ブロック2個
            // Head block of two and a following block of two
            Assert.AreEqual(2, GetBlockSizeAt(segment, 0));
            Assert.AreEqual(2, GetBlockSizeAt(segment, 1));
            Assert.AreEqual(2, GetBlockSizeAt(segment, 2));
            Assert.AreEqual(2, GetBlockSizeAt(segment, 3));
            Assert.AreEqual(1, segment.CaptureItems()[0].Item.ItemInstanceId.AsPrimitive());
        }

        [Test]
        public void 空のsegmentは占有長も搬出長も0()
        {
            var segment = CreateNormal(3, 0);
            Assert.AreEqual(0, segment.Count);
            Assert.AreEqual(0, GetTotalLength(segment));
            Assert.AreEqual(0, GetOutputLength(segment, BeltConstants.MaxSpeed));
            Assert.AreEqual(0, segment.CaptureItems().Length);
        }

        [Test]
        public void 搬出長は速度から出口までの距離を引いた値()
        {
            var segment = CreateNormal(2, 0);
            EnqueueTail(segment, 50, MakeItem(1));
            Assert.AreEqual(78, GetOutputLength(segment, 128));
            Assert.AreEqual(-10, GetOutputLength(segment, 40));
        }

        [Test]
        public void 捕捉して新しい走行列へ復元すると同じ状態になる()
        {
            var source = CreateNormal(6, 0);
            EnqueueTail(source, 0, MakeItem(1));
            EnqueueTail(source, 0, MakeItem(2));
            EnqueueTail(source, 17, MakeItem(3));
            EnqueueTail(source, 200, MakeItem(4));
            EnqueueTail(source, 0, MakeItem(5));
            var captured = source.CaptureItems();

            var restored = CreateNormal(6, 0);
            restored.RestoreItems(captured);
            var recaptured = restored.CaptureItems();

            Assert.AreEqual(captured.Length, recaptured.Length);
            for (var i = 0; i < captured.Length; i++)
            {
                Assert.AreEqual(captured[i].DistanceToExit, recaptured[i].DistanceToExit, $"distance[{i}]");
                Assert.AreEqual(captured[i].Item.ItemInstanceId, recaptured[i].Item.ItemInstanceId, $"item[{i}]");
            }
            Assert.AreEqual(GetTotalLength(source), GetTotalLength(restored));
            AssertStructure(restored);
            AssertDistances(restored, 0, W, 17 + 2 * W, 217 + 3 * W, 217 + 4 * W);
        }

        [Test]
        public void 容量が範囲外なら例外()
        {
            var maxCapacity = (int.MaxValue - (W - 1)) / W;
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateNormal(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateNormal(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateNormal(maxCapacity + 1, 0));
            Assert.AreEqual(1, CreateNormal(1, 0).Capacity);
        }
    }
}
