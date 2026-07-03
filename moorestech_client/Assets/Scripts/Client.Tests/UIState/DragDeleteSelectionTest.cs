using Client.Game.InGame.UI.UIState.State.DragDelete;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     DragDeleteSelectionの選択ロジックを検証するテスト
    ///     Tests verifying the selection logic of DragDeleteSelection
    /// </summary>
    public class DragDeleteSelectionTest
    {
        [Test]
        public void AddRemovableTargetAddsAndPreviews()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);

            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void AddSameTargetTwiceDoesNotDoubleAdd()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.TryAddTarget(target, out _);

            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void AddNonRemovableTargetIsIgnored()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = false };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.CommitDelete();

            Assert.AreEqual(0, target.SetPreviewCount);
            Assert.AreEqual(0, target.DeleteCount);
        }

        [Test]
        public void CancelSelectionResetsAndDisablesCommit()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.CancelSelection();

            Assert.AreEqual(1, target.ResetCount);
            Assert.IsFalse(selection.CanCommit());
        }

        [Test]
        public void CommitDeleteDeletesTargetThenClears()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.CommitDelete();
            // クリア済みなので二度目のCommitは何もしない
            // The selection is cleared, so a second commit deletes nothing more
            selection.CommitDelete();

            Assert.AreEqual(1, target.DeleteCount);
        }

        [Test]
        public void BeginDragAfterCancelReenablesCommit()
        {
            var selection = new DragDeleteSelection();
            var first = new FakeDeleteTarget { Removable = true };
            var second = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(first, out _);
            selection.CancelSelection();

            // 再ドラッグでキャンセルフラグが解除され、再び選択・確定できる
            // A fresh drag clears the canceled flag so selecting and committing works again
            selection.BeginDrag();
            Assert.IsTrue(selection.CanCommit());
            selection.TryAddTarget(second, out _);
            selection.CommitDelete();

            Assert.AreEqual(1, second.DeleteCount);
            Assert.AreEqual(0, first.DeleteCount);
        }

        [Test]
        public void AddTargetAfterCancelIsIgnored()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.CancelSelection();
            selection.TryAddTarget(target, out _);

            Assert.AreEqual(0, target.SetPreviewCount);
        }

        [Test]
        public void CommitDeleteAfterCancelWithoutBeginDragDeletesNothing()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.CancelSelection();
            selection.CommitDelete();

            Assert.AreEqual(0, target.DeleteCount);
        }

        [Test]
        public void CommitDeleteDeletesEachOfTwoTargets()
        {
            var selection = new DragDeleteSelection();
            var first = new FakeDeleteTarget { Removable = true };
            var second = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(first, out _);
            selection.TryAddTarget(second, out _);
            selection.CommitDelete();

            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(1, second.DeleteCount);
        }

        [Test]
        public void CommitDeleteResetsMaterialOncePerTarget()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.TryAddTarget(target, out _);
            selection.CommitDelete();

            Assert.AreEqual(1, target.ResetCount);
        }

        [Test]
        public void SameLogicalKeyTargetsAreDedupedToOne()
        {
            // 同一論理対象（同じキー）の別ラッパーは1件に集約され重複Deleteしない
            // Different wrappers of the same logical target (same key) collapse into one, no duplicate Delete
            var selection = new DragDeleteSelection();
            var sharedKey = new object();
            var first = new FakeDeleteTarget { Removable = true, Key = sharedKey };
            var second = new FakeDeleteTarget { Removable = true, Key = sharedKey };

            selection.BeginDrag();
            selection.TryAddTarget(first, out _);
            selection.TryAddTarget(second, out _);
            selection.CommitDelete();

            Assert.AreEqual(1, first.SetPreviewCount);
            Assert.AreEqual(0, second.SetPreviewCount);
            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(0, second.DeleteCount);
        }
    }
}
