using System;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Connection.BeltConnectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 通常segmentの搬入・搬出口。期待値は手計算
    // Input and output ports of a normal segment. Expected values are hand-computed
    public class BeltConveyorSegmentPortTest
    {
        [Test]
        public void 空きは長さから占有長を引いた値で最後尾がはみ出す間は負()
        {
            var segment = CreateNormal(3, 0);
            Assert.AreEqual(3 * W, segment.GetOffer(BeltDirection.Back));
            EnqueueTail(segment, 100, MakeItem(1));
            Assert.AreEqual(2 * W - 100, segment.GetOffer(BeltDirection.Back));

            // 進入距離100で搬入した直後は後端が入口から156はみ出す
            // Right after entering by 100 the rear sticks out of the entrance by 156
            var entering = CreateNormal(2, 0);
            Assert.IsTrue(entering.TryReceive(BeltDirection.Back, 100, MakeItem(2)));
            AssertDistances(entering, 2 * W - 100);
            Assert.AreEqual(100 - W, entering.GetOffer(BeltDirection.Back));
            Advance(entering, 100, false);
            Assert.AreEqual(200 - W, entering.GetOffer(BeltDirection.Back));
            Advance(entering, 56, false);
            Assert.AreEqual(0, entering.GetOffer(BeltDirection.Back));
            Advance(entering, 100, false);
            Assert.AreEqual(100, entering.GetOffer(BeltDirection.Back));
        }

        [Test]
        public void 空きを超える進入距離は拒否し収まれば進入距離を保って末尾へ置く()
        {
            var segment = CreateNormal(3, 0);
            EnqueueTail(segment, 0, MakeItem(1));
            Assert.IsFalse(segment.TryReceive(BeltDirection.Front, 2 * W + 1, MakeItem(2)));
            AssertDistances(segment, 0);

            // 空き2Wへ進入距離100で入ると、後端は入口の外100の位置になる
            // Entering an offer of 2W by 100 leaves the rear 100 outside the entrance
            Assert.IsTrue(segment.TryReceive(BeltDirection.Left, 100, MakeItem(2)));
            AssertDistances(segment, 0, 3 * W - 100);
            Assert.AreEqual(2, segment.CaptureItems()[1].Item.ItemInstanceId.AsPrimitive());

            // 空きちょうどの進入距離は受け入れ、空き0では1でも拒否する
            // An entry length equal to the offer is accepted; with zero offer even 1 is rejected
            var single = CreateNormal(1, 0);
            Assert.IsTrue(single.TryReceive(BeltDirection.Right, W, MakeItem(3)));
            AssertDistances(single, 0);
            Assert.IsFalse(single.TryReceive(BeltDirection.Right, 1, MakeItem(4)));
            Assert.AreEqual(1, single.Count);
        }

        [Test]
        public void 接続すると相手へ搬出方向の反対で自分を登録する()
        {
            var segment = CreateNormal(1, 0);
            var receiver = new FakeBeltReceiver(W, true);
            segment.ConnectTo(receiver, BeltDirection.Right, BeltEntryDirections.Level(BeltDirection.Left));
            Assert.AreSame(receiver, segment.Output);
            Assert.AreEqual(1, receiver.AttachedInputs.Count);
            Assert.AreSame(segment, receiver.AttachedInputs[0].Source);
            Assert.AreEqual(BeltDirection.Left, receiver.AttachedInputs[0].Direction);
        }

        [Test]
        public void 先頭が出口を越えると越えた距離で搬出し後続は速度分進む()
        {
            var segment = CreateNormal(2, 0);
            EnqueueTail(segment, 30, MakeItem(1));
            EnqueueTail(segment, 10, MakeItem(2));
            var receiver = new FakeBeltReceiver(W, true);
            segment.ConnectTo(receiver, BeltDirection.Right, BeltEntryDirections.Level(BeltDirection.Left));

            BeginTickAt(segment, 100);
            AdvanceAndTransfer(segment);
            Assert.AreEqual(1, receiver.ReceiveAttempts.Count);
            Assert.AreEqual(BeltDirection.Left, receiver.ReceiveAttempts[0].Direction);
            Assert.AreEqual(70, receiver.ReceiveAttempts[0].Length);
            Assert.AreEqual(1, receiver.ReceiveAttempts[0].Item.ItemInstanceId.AsPrimitive());
            AssertDistances(segment, 296 - 100);
        }

        [Test]
        public void 搬出先が拒否すると先頭は出口で止まり後続が詰まる()
        {
            // 出口から30・336・792。速度100で先頭停止、残り70で隙間50を詰め、残り20で隙間200が180になる
            // At 30, 336 and 792. Speed 100 parks the head, 70 closes the 50 gap, and 20 shrinks the 200 gap to 180
            var segment = CreateNormal(3, 0);
            EnqueueTail(segment, 30, MakeItem(1));
            EnqueueTail(segment, 50, MakeItem(2));
            EnqueueTail(segment, 200, MakeItem(3));
            var receiver = new FakeBeltReceiver(W, false);
            segment.ConnectTo(receiver, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));

            BeginTickAt(segment, 100);
            AdvanceAndTransfer(segment);
            Assert.AreEqual(1, receiver.ReceiveAttempts.Count);
            Assert.AreEqual(BeltDirection.Back, receiver.ReceiveAttempts[0].Direction);
            Assert.AreEqual(70, receiver.ReceiveAttempts[0].Length);
            AssertDistances(segment, 0, W, 2 * W + 180);
        }

        [Test]
        public void 出口を越えないか搬出先が無ければ搬出を試さない()
        {
            var segment = CreateNormal(2, 0);
            EnqueueTail(segment, 100, MakeItem(1));
            var receiver = new FakeBeltReceiver(W, true);
            segment.ConnectTo(receiver, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));

            // 速度が出口までの距離ちょうどなら進入距離0で搬出しない
            // A speed exactly equal to the distance gives entry length 0 and no output
            BeginTickAt(segment, 50);
            AdvanceAndTransfer(segment);
            BeginTickAt(segment, 50);
            AdvanceAndTransfer(segment);
            Assert.AreEqual(0, receiver.ReceiveAttempts.Count);
            AssertDistances(segment, 0);

            var unconnected = CreateNormal(2, 0);
            EnqueueTail(unconnected, 30, MakeItem(2));
            BeginTickAt(unconnected, 100);
            AdvanceAndTransfer(unconnected);
            AssertDistances(unconnected, 0);
        }

        [Test]
        public void 搬出可否はこのtickで先頭が出口を越えるかで答える()
        {
            var segment = CreateNormal(2, 0);
            BeginTickAt(segment, BeltConstants.MaxSpeed);
            Assert.IsFalse(segment.TryGetOutput(BeltDirection.Back));

            EnqueueTail(segment, 30, MakeItem(1));
            BeginTickAt(segment, 30);
            Assert.IsFalse(segment.TryGetOutput(BeltDirection.Back));
            BeginTickAt(segment, 31);
            Assert.IsTrue(segment.TryGetOutput(BeltDirection.Back));
            Assert.IsTrue(segment.TryGetOutput(BeltDirection.Left));
        }

        [Test]
        public void 生成時に容量と種類と速度を検証する()
        {
            var order = BeltPriority.InitializeFromDirection;
            // 合流の容量は1固定、種類は型で決まるため、容量と速度だけを引数で検証する
            // A merge is fixed at one cell and the kind is the type, so only capacity and speed arguments are validated
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeltBranchSegment(0, 0, order, BeltDirection.Front));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeltNormalSegment(1, BeltConstants.MaxSpeed + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeltNormalSegment(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeltMergeSegment(-1, order, BeltDirection.Front));

            Assert.AreEqual(BeltSegmentKind.Merge, new BeltMergeSegment(0, order, BeltDirection.Front).Kind);
            Assert.AreEqual(1, new BeltMergeSegment(0, order, BeltDirection.Front).Capacity);
            Assert.AreEqual(BeltSegmentKind.Branch, new BeltBranchSegment(4, 0, order, BeltDirection.Front).Kind);
            Assert.AreEqual(BeltSegmentKind.Normal, CreateNormal(4, BeltConstants.MaxSpeed).Kind);
        }
    }
}
