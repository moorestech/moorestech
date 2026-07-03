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

            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void AddNonRemovableTargetIsIgnored()
        {
            var selection = new DragDeleteSelection();
            var target = new FakeDeleteTarget { Removable = false };

            selection.BeginDrag();
            selection.AddTarget(target);
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
            selection.AddTarget(target);
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
            selection.AddTarget(target);
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
            selection.AddTarget(first);
            selection.CancelSelection();

            // 再ドラッグでキャンセルフラグが解除され、再び選択・確定できる
            // A fresh drag clears the canceled flag so selecting and committing works again
            selection.BeginDrag();
            Assert.IsTrue(selection.CanCommit());
            selection.AddTarget(second);
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
            selection.AddTarget(target);

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
            selection.AddTarget(first);
            selection.AddTarget(second);
            selection.CommitDelete();

            Assert.AreEqual(1, first.SetPreviewCount);
            Assert.AreEqual(0, second.SetPreviewCount);
            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(0, second.DeleteCount);
        }

        [Test]
        public void DefaultStartAllowsMultipleDefaultTargets()
        {
            // default開始ではdefault同士を複数選択できる
            // A default-started session can multi-select default targets
            var selection = new DragDeleteSelection();
            var first = new FakeDeleteTarget { Removable = true, Category = "default" };
            var second = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            selection.AddTarget(first);
            selection.AddTarget(second);
            selection.CommitDelete();

            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(1, second.DeleteCount);
        }

        [Test]
        public void DefaultStartRejectsFoundationTarget()
        {
            // default開始では土台カテゴリーを追加選択できない
            // A default-started session cannot add a foundation-category target
            var selection = new DragDeleteSelection();
            var start = new FakeDeleteTarget { Removable = true, Category = "default" };
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };

            selection.BeginDrag();
            selection.AddTarget(start);
            Assert.IsFalse(selection.IsCategoryCompatible(foundation));
            selection.AddTarget(foundation);
            selection.CommitDelete();

            Assert.AreEqual(1, start.DeleteCount);
            Assert.AreEqual(0, foundation.DeleteCount);
        }

        [Test]
        public void FoundationStartRejectsDefaultTarget()
        {
            // 土台開始では土台だけ選択でき、defaultは追加選択できない
            // A foundation-started session accepts only foundation, not default
            var selection = new DragDeleteSelection();
            var foundationA = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var foundationB = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            selection.AddTarget(foundationA);
            selection.AddTarget(foundationB);
            selection.AddTarget(defaultTarget);
            selection.CommitDelete();

            Assert.AreEqual(1, foundationA.DeleteCount);
            Assert.AreEqual(1, foundationB.DeleteCount);
            Assert.AreEqual(0, defaultTarget.DeleteCount);
        }

        [Test]
        public void CategoryLockResetsAfterCommit()
        {
            // 破壊完了後は次のセッションへカテゴリー固定が漏れない
            // The category lock does not leak into the next session after a commit
            var selection = new DragDeleteSelection();
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            selection.AddTarget(foundation);
            selection.CommitDelete();

            selection.BeginDrag();
            Assert.IsTrue(selection.IsCategoryCompatible(defaultTarget));
            selection.AddTarget(defaultTarget);
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
        }

        [Test]
        public void CategoryLockResetsAfterCancel()
        {
            // キャンセル後は次のセッションへカテゴリー固定が漏れない
            // The category lock does not leak into the next session after a cancel
            var selection = new DragDeleteSelection();
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            selection.AddTarget(foundation);
            selection.CancelSelection();

            selection.BeginDrag();
            Assert.IsTrue(selection.IsCategoryCompatible(defaultTarget));
            selection.AddTarget(defaultTarget);
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
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
            public object Key;
            public string Category = "default";

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

            public object GetDeleteTargetKey()
            {
                // Key未指定なら自身を一意キーとする（既存テストは個別インスタンス＝個別キー）
                // Default to self as the unique key when Key is unset (existing tests use per-instance keys)
                return Key ?? this;
            }

            public string GetDestructionCategory()
            {
                return Category;
            }
        }
    }
}
