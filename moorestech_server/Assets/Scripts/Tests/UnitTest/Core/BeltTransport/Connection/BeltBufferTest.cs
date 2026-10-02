using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Connection.BeltConnectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 合流・分岐の終端buffer。回収・搬出・優先順。期待値は手計算
    // The end buffer of merges and branches: collect, transfer and priority order. Expected values are hand-computed
    public class BeltBufferTest
    {
        private const int Init = BeltPriority.InitializeFromDirection;

        [Test]
        public void 出口でクランプし先頭が出口ちょうどでbufferが空のときだけ回収する()
        {
            var branch = CreateBranch(2, Init, BeltDirection.Front);
            EnqueueTail(branch, 100, MakeItem(1));
            EnqueueTail(branch, 0, MakeItem(2));

            BeginTickAt(branch, 50);
            Collect(branch.Buffer);
            Assert.IsFalse(branch.Buffer.HasItem);
            AssertDistances(branch, 50, 50 + W);

            // 先頭は出口で止まって回収され、後続は回収後に追加で進まない
            // The head stops at the exit and is collected; the follower does not advance further afterwards
            BeginTickAt(branch, 128);
            Collect(branch.Buffer);
            Assert.IsTrue(branch.Buffer.TryGetItem(out var collected));
            Assert.AreEqual(1, collected.ItemInstanceId.AsPrimitive());
            AssertDistances(branch, W);

            // bufferが満杯なら出口に着いても回収しない
            // With a full buffer the head is not collected even at the exit
            BeginTickAt(branch, 128);
            Collect(branch.Buffer);
            BeginTickAt(branch, 128);
            Collect(branch.Buffer);
            AssertDistances(branch, 0);
            Assert.IsTrue(branch.Buffer.TryGetItem(out var held));
            Assert.AreEqual(1, held.ItemInstanceId.AsPrimitive());
        }

        [Test]
        public void 速度0でも出口にあるアイテムは回収する()
        {
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            EnqueueTail(branch, 0, MakeItem(1));
            BeginTickAt(branch, 0);
            Collect(branch.Buffer);
            Assert.IsTrue(branch.Buffer.HasItem);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void 分岐は成功した方向を優先順の末尾へ移す()
        {
            var branch = CreateBranch(1, Order(BeltDirection.Front, BeltDirection.Back, BeltDirection.Left), BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var back = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, true);
            branch.Buffer.ConnectTo(front, BeltDirection.Front);
            branch.Buffer.ConnectTo(back, BeltDirection.Back);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
            Assert.AreSame(branch.Buffer, front.AttachedInputs[0].Source);
            Assert.AreEqual(BeltDirection.Right, left.AttachedInputs[0].Direction);
            BeginTickAt(branch, 100);

            // (0,1,2)で0が成功すると(1,2,0)
            // From (0,1,2), success on 0 gives (1,2,0)
            TransferRestored(branch, 1);
            Assert.AreEqual(1, front.ReceivedItems.Count);
            Assert.AreEqual(BeltDirection.Back, front.ReceiveAttempts[0].Direction);
            Assert.AreEqual(100, front.ReceiveAttempts[0].Length);
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Left, BeltDirection.Front), branch.PriorityOrder);

            // (1,2,0)で1が詰まり2が成功すると(1,0,2)
            // From (1,2,0), 1 blocked and 2 succeeding gives (1,0,2)
            back.SetAccepts(false);
            TransferRestored(branch, 2);
            Assert.AreEqual(1, back.ReceiveAttempts.Count);
            Assert.AreEqual(1, left.ReceivedItems.Count);
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Front, BeltDirection.Left), branch.PriorityOrder);

            // (1,0,2)で末尾の2だけが成功しても順序は変わらない
            // From (1,0,2), success on the last direction 2 keeps the order
            front.SetAccepts(false);
            TransferRestored(branch, 3);
            Assert.AreEqual(2, left.ReceivedItems.Count);
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Front, BeltDirection.Left), branch.PriorityOrder);
            Assert.IsFalse(branch.Buffer.HasItem);
        }

        [Test]
        public void 未接続方向と空きの無い方向を飛ばし速度と空きの小さい方で搬出する()
        {
            // 初期順(Front,Left,Right)。Frontは未接続、Leftは空き0
            // Initial order (Front,Left,Right). Front is unconnected, Left has zero offer
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            var right = new FakeBeltReceiver(30, true);
            var left = new FakeBeltReceiver(0, true);
            branch.Buffer.ConnectTo(right, BeltDirection.Right);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
            BeginTickAt(branch, 100);

            TransferRestored(branch, 1);
            Assert.AreEqual(0, left.ReceiveAttempts.Count);
            Assert.AreEqual(1, right.ReceiveAttempts.Count);
            Assert.AreEqual(BeltDirection.Left, right.ReceiveAttempts[0].Direction);
            Assert.AreEqual(30, right.ReceiveAttempts[0].Length);
            Assert.AreEqual(Order(BeltDirection.Front, BeltDirection.Left, BeltDirection.Right), branch.PriorityOrder);

            left.SetOffer(W);
            TransferRestored(branch, 2);
            Assert.AreEqual(100, left.ReceiveAttempts[0].Length);
            Assert.AreEqual(Order(BeltDirection.Front, BeltDirection.Right, BeltDirection.Left), branch.PriorityOrder);
        }

        [Test]
        public void 速度0のtickは搬出しない()
        {
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            branch.Buffer.ConnectTo(front, BeltDirection.Front);
            BeginTickAt(branch, 0);
            Assert.AreEqual(0, GetTickSpeed(branch));

            TransferRestored(branch, 1);
            Assert.AreEqual(0, front.ReceiveAttempts.Count);
            Assert.IsTrue(branch.Buffer.HasItem);
        }

        [Test]
        public void 合流bufferの搬出先は1つで優先順は変わらない()
        {
            var merge = CreateMerge(Init, BeltDirection.Right);
            Assert.AreEqual((int)BeltDirection.Right, GetBufferPriorityOrder(merge.Buffer));
            var left = new FakeBeltReceiver(W, true);
            merge.Buffer.ConnectTo(left, BeltDirection.Left);
            Assert.AreEqual((int)BeltDirection.Left, GetBufferPriorityOrder(merge.Buffer));
            BeginTickAt(merge, 64);

            TransferRestored(merge, 1);
            TransferRestored(merge, 2);
            Assert.AreEqual(2, left.ReceivedItems.Count);
            Assert.AreEqual(64, left.ReceiveAttempts[1].Length);
            Assert.AreEqual((int)BeltDirection.Left, GetBufferPriorityOrder(merge.Buffer));
        }

        [Test]
        public void 通常segmentへ搬出すると進入距離を保って入口に置かれる()
        {
            // 空き256に速度100で入り、詰まった2個目は相手の空きが負なので保持したまま
            // Enters an offer of 256 by speed 100; the second stays held because the target's offer is negative
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            var normal = CreateNormal(1, 0);
            branch.Buffer.ConnectTo(normal, BeltDirection.Front);
            BeginTickAt(branch, 100);
            TransferRestored(branch, 1);
            AssertDistances(normal, W - 100);

            TransferRestored(branch, 2);
            Assert.IsTrue(branch.Buffer.HasItem);
            Assert.AreEqual(1, normal.Count);
        }

        [Test]
        public void 保持アイテムの読み取りと復元()
        {
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            Assert.IsFalse(branch.Buffer.TryGetItem(out var empty));
            Assert.AreEqual(0, empty.ItemInstanceId.AsPrimitive());
            Assert.AreSame(branch, branch.Buffer.Segment);

            branch.Buffer.RestoreItem(MakeItem(7));
            Assert.IsTrue(branch.Buffer.TryGetItem(out var restored));
            Assert.AreEqual(7, restored.ItemInstanceId.AsPrimitive());
            Assert.IsTrue(branch.Buffer.TryGetItem(out var again));
            Assert.AreEqual(7, again.ItemInstanceId.AsPrimitive());
        }

        [Test]
        public void 搬出可否は接続済みの最優先方向にだけ答える()
        {
            // 初期順(Front,Left,Right)でFrontは未接続なので、最優先はLeft
            // Initial order (Front,Left,Right) with Front unconnected, so Left is the highest priority
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            branch.Buffer.ConnectTo(new FakeBeltReceiver(W, true), BeltDirection.Right);
            branch.Buffer.ConnectTo(new FakeBeltReceiver(0, false), BeltDirection.Left);
            Assert.IsFalse(branch.Buffer.TryGetOutput(BeltDirection.Right));

            branch.Buffer.RestoreItem(MakeItem(1));
            BeginTickAt(branch, 64);
            Assert.IsTrue(branch.Buffer.TryGetOutput(BeltDirection.Right));
            Assert.IsFalse(branch.Buffer.TryGetOutput(BeltDirection.Left));
            Assert.IsFalse(branch.Buffer.TryGetOutput(BeltDirection.Back));

            // そのtickの速度が0なら、最優先方向でも搬出不可と答える
            // At tick speed 0 it answers no even for the highest-priority direction
            BeginTickAt(branch, 0);
            Assert.IsFalse(branch.Buffer.TryGetOutput(BeltDirection.Right));
        }

        [Test]
        public void 優先順は保存値が無ければ役割と向きから初期化する()
        {
            Assert.AreEqual(Order(BeltDirection.Front, BeltDirection.Left, BeltDirection.Right), CreateBranch(2, Init, BeltDirection.Front).PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Back), CreateBranch(2, Init, BeltDirection.Left).PriorityOrder);
            var saved = Order(BeltDirection.Right, BeltDirection.Back, BeltDirection.Left);
            Assert.AreEqual(saved, CreateBranch(2, saved, BeltDirection.Front).PriorityOrder);

            // 合流は唯一の搬出方向の反対側（直進の搬入）を先頭にする
            // A merge starts with the side opposite its only output, the straight input
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Left, BeltDirection.Right), CreateMerge(Init, BeltDirection.Front).PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Back), CreateMerge(Init, BeltDirection.Right).PriorityOrder);
            Assert.AreEqual(saved, CreateMerge(saved, BeltDirection.Front).PriorityOrder);

            BeltConveyorSegment normal = CreateNormal(2, 0);
            Assert.AreEqual(0, normal.PriorityOrder);
            Assert.IsNotInstanceOf<BeltBufferedSegment>(normal);
        }

        private static void TransferRestored(BeltBufferedSegment segment, long serial)
        {
            segment.Buffer.RestoreItem(MakeItem(serial));
            Transfer(segment.Buffer);
        }
    }
}
