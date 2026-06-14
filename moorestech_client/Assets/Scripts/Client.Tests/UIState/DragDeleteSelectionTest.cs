using Client.Game.InGame.UI.UIState.State;
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
            selection.AddTarget(target);

            Assert.AreEqual(1, selection.SelectedCount());
            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void AddSameTargetTwiceDoesNotDoubleAdd()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.AddTarget(target);
            selection.AddTarget(target);

            Assert.AreEqual(1, selection.SelectedCount());
            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void AddNonRemovableTargetIsIgnored()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = false };

            selection.BeginDrag();
            selection.AddTarget(target);

            Assert.AreEqual(0, selection.SelectedCount());
            Assert.AreEqual(0, target.SetPreviewCount);
        }

        [Test]
        public void CancelSelectionResetsAndDisablesCommit()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.AddTarget(target);
            selection.CancelSelection();

            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(0, selection.SelectedCount());
            Assert.IsFalse(selection.CanCommit());
        }

        [Test]
        public void CommitDeleteDeletesEachAndClears()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.AddTarget(target);
            selection.CommitDelete();

            Assert.AreEqual(1, target.DeleteCount);
            Assert.AreEqual(0, selection.SelectedCount());
        }

        [Test]
        public void BeginDragAfterCancelClearsCanceledFlag()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.AddTarget(target);
            selection.CancelSelection();
            selection.BeginDrag();

            Assert.IsTrue(selection.CanCommit());
            Assert.AreEqual(0, selection.SelectedCount());
        }

        [Test]
        public void AddTargetAfterCancelIsIgnored()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.CancelSelection();
            selection.AddTarget(target);

            Assert.AreEqual(0, selection.SelectedCount());
            Assert.AreEqual(0, target.SetPreviewCount);
        }

        [Test]
        public void CommitDeleteAfterCancelWithoutBeginDragDeletesNothing()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = true };

            selection.BeginDrag();
            selection.AddTarget(target);
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
            selection.AddTarget(first);
            selection.AddTarget(second);
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
            selection.AddTarget(target);
            selection.CommitDelete();

            Assert.AreEqual(1, target.ResetCount);
        }

        /// <summary>
        ///     呼び出し回数を記録するIDeleteTargetのテスト用実装
        ///     Test implementation of IDeleteTarget that records call counts
        /// </summary>
        private class FakeDeleteTarget : IDeleteTarget
        {
            public int SetPreviewCount;
            public int ResetCount;
            public int DeleteCount;
            public bool Removable;

            public void SetRemovePreviewing()
            {
                SetPreviewCount++;
            }

            public void ResetMaterial()
            {
                ResetCount++;
            }

            public bool IsRemovable(out string reason)
            {
                reason = null;
                return Removable;
            }

            public void Delete()
            {
                DeleteCount++;
            }
        }
    }
}
