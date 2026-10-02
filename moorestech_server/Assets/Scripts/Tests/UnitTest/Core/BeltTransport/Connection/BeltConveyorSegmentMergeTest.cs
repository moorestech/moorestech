using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Connection.BeltConnectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 合流segmentの段階2予約と、予約方向だけの搬入・搬入優先順の回転
    // Stage-2 reservation of a merge, input only from the reserved direction, and input order rotation
    public class BeltConveyorSegmentMergeTest
    {
        private const int Init = BeltPriority.InitializeFromDirection;

        [Test]
        public void 空の合流は搬入優先順で最初に搬出可能な搬入元を予約する()
        {
            // 前向きの合流の初期順は(Back,Left,Right)。登録順はそれと変えておく
            // A front-facing merge starts with (Back,Left,Right). Register in a different order
            var merge = CreateMerge(Init, BeltDirection.Front);
            var left = new FakeBeltSource(true);
            var right = new FakeBeltSource(true);
            var back = new FakeBeltSource(false);
            merge.AttachInput(left, BeltDirection.Left);
            merge.AttachInput(right, BeltDirection.Right);
            merge.AttachInput(back, BeltDirection.Back);

            ResolveInput(merge);
            CollectionAssert.AreEqual(new[] { BeltDirection.Back }, back.Queries);
            CollectionAssert.AreEqual(new[] { BeltDirection.Left }, left.Queries);
            Assert.AreEqual(0, right.Queries.Count);
            AssertOnlyReserved(merge, BeltDirection.Left);

            // 次の予約は前回の予約を消してから選び直す
            // The next reservation clears the previous one before choosing again
            left.SetHasOutput(false);
            ResolveInput(merge);
            AssertOnlyReserved(merge, BeltDirection.Right);
            right.SetHasOutput(false);
            ResolveInput(merge);
            AssertOnlyReserved(merge, BeltDirection.None);
        }

        [Test]
        public void 空でない合流は何も予約せず問い合わせもしない()
        {
            var merge = CreateMerge(Init, BeltDirection.Front);
            var back = new FakeBeltSource(true);
            merge.AttachInput(back, BeltDirection.Back);
            ResolveInput(merge);
            AssertOnlyReserved(merge, BeltDirection.Back);

            EnqueueTail(merge, 0, MakeItem(1));
            ResolveInput(merge);
            Assert.AreEqual(1, back.Queries.Count);
            Assert.AreEqual(0, merge.GetOffer(BeltDirection.Back));
        }

        [Test]
        public void 最優先出力がこの合流でないbufferは飛ばして次の搬入元を予約する()
        {
            // 分岐bufferは合流へRightで接続、通常segmentは合流へLeftで接続する。合流から見るとLeftが分岐、Rightが通常
            // The branch buffer connects to the merge via Right and the normal via Left; seen from the merge, Left is the branch and Right the normal
            var merge = CreateMerge(Init, BeltDirection.Front);
            var branch = CreateBranch(1, Init, BeltDirection.Front);
            branch.Buffer.ConnectTo(merge, BeltDirection.Right);
            var normal = CreateNormal(1, 0);
            EnqueueTail(normal, 0, MakeItem(1));
            normal.ConnectTo(merge, BeltDirection.Left);
            BeginTickAt(normal, 64);
            branch.Buffer.RestoreItem(MakeItem(2));

            // 分岐の最優先接続がFrontの別の搬出先なら、分岐は合流に提示しない
            // When the branch's top connected output is another target at Front, it does not offer to the merge
            branch.Buffer.ConnectTo(new FakeBeltReceiver(0, false), BeltDirection.Front);
            ResolveInput(merge);
            AssertOnlyReserved(merge, BeltDirection.Right);

            var mergeWithBranchFirst = CreateMerge(Init, BeltDirection.Front);
            var branchToMergeOnly = CreateBranch(1, Init, BeltDirection.Front);
            branchToMergeOnly.Buffer.ConnectTo(mergeWithBranchFirst, BeltDirection.Right);
            branchToMergeOnly.Buffer.RestoreItem(MakeItem(3));
            BeginTickAt(branchToMergeOnly, 100);
            ResolveInput(mergeWithBranchFirst);
            AssertOnlyReserved(mergeWithBranchFirst, BeltDirection.Left);

            // 予約に従って分岐bufferから合流へ搬出できる
            // The branch buffer can then transfer into the merge as reserved
            Transfer(branchToMergeOnly.Buffer);
            Assert.IsFalse(branchToMergeOnly.Buffer.HasItem);
            AssertDistances(mergeWithBranchFirst, W - 100);
        }

        [Test]
        public void 同じtickでは予約した方向からだけ受け入れる()
        {
            var merge = CreateMerge(Init, BeltDirection.Front);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Left);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Right);
            ResolveInput(merge);

            Assert.IsFalse(merge.TryReceive(BeltDirection.Right, 10, MakeItem(1)));
            Assert.AreEqual(0, merge.Count);
            Assert.IsTrue(merge.TryReceive(BeltDirection.Left, 10, MakeItem(2)));
            AssertDistances(merge, W - 10);
        }

        [Test]
        public void 搬入に成功した方向を末尾へ移し失敗では順序を変えない()
        {
            var merge = CreateMerge(Init, BeltDirection.Front);
            var back = new FakeBeltSource(false);
            merge.AttachInput(back, BeltDirection.Back);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Left);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Right);
            ResolveInput(merge);

            // 予約外の方向と空き超過の失敗では順序を保つ
            // Failures from an unreserved direction or an oversized entry keep the order
            Assert.IsFalse(merge.TryReceive(BeltDirection.Right, 10, MakeItem(1)));
            Assert.IsFalse(merge.TryReceive(BeltDirection.Left, W + 1, MakeItem(1)));
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Left, BeltDirection.Right), merge.PriorityOrder);

            Assert.IsTrue(merge.TryReceive(BeltDirection.Left, 10, MakeItem(1)));
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Right, BeltDirection.Left), merge.PriorityOrder);

            // 先頭方向で成功すると先頭が末尾へ回る
            // Success on the first direction rotates it to the end
            DequeueHead(merge);
            back.SetHasOutput(true);
            ResolveInput(merge);
            Assert.IsTrue(merge.TryReceive(BeltDirection.Back, 10, MakeItem(2)));
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Back), merge.PriorityOrder);
        }

        [Test]
        public void 初期順は唯一の搬出方向の反対側を先頭にする()
        {
            // 右向きの合流は直進のLeftを最優先にする
            // A right-facing merge gives the straight input Left the highest priority
            var merge = CreateMerge(Init, BeltDirection.Right);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Front);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Back);
            merge.AttachInput(new FakeBeltSource(true), BeltDirection.Left);
            ResolveInput(merge);
            AssertOnlyReserved(merge, BeltDirection.Left);
        }

        // 予約方向だけが空き1マス分を返し、それ以外の3方向は0を返す
        // Only the reserved direction offers one cell; the other directions offer 0
        private static void AssertOnlyReserved(BeltConveyorSegment merge, BeltDirection reserved)
        {
            foreach (var direction in new[] { BeltDirection.Front, BeltDirection.Back, BeltDirection.Left, BeltDirection.Right })
                Assert.AreEqual(direction == reserved ? W : 0, merge.GetOffer(direction), $"offer from {direction}");
        }
    }
}
