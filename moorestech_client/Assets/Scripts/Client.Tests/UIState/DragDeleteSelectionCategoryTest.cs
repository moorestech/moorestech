using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     破壊カテゴリーによる同時選択制限を検証するテスト
    ///     Tests verifying the destruction-category restriction on simultaneous selection
    /// </summary>
    public class DragDeleteSelectionCategoryTest
    {
        [Test]
        public void DefaultStartAllowsMultipleDefaultTargets()
        {
            // default開始ではdefault同士を複数選択できる
            // A default-started session can multi-select default targets
            var selection = new DragDeleteSelection(new BuildOperationHistory());
            var first = new FakeDeleteTarget { Removable = true, Category = "default" };
            var second = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(first, out _));
            Assert.IsTrue(selection.TryAddTarget(second, out _));
            selection.CommitDelete();

            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(1, second.DeleteCount);
        }

        [Test]
        public void DefaultStartRejectsFoundationTarget()
        {
            // default開始では土台カテゴリーを追加選択できず、拒否理由が返る
            // A default-started session rejects a foundation target and returns the deny reason
            var selection = new DragDeleteSelection(new BuildOperationHistory());
            var start = new FakeDeleteTarget { Removable = true, Category = "default" };
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(start, out _));
            Assert.IsFalse(selection.TryAddTarget(foundation, out var denyReason));
            Assert.AreEqual(DragDeleteSelection.DifferentCategoryDenyReason, denyReason);
            selection.CommitDelete();

            Assert.AreEqual(1, start.DeleteCount);
            Assert.AreEqual(0, foundation.DeleteCount);
        }

        [Test]
        public void FoundationStartRejectsDefaultTarget()
        {
            // 土台開始では土台だけ選択でき、defaultは追加選択できない
            // A foundation-started session accepts only foundation, not default
            var selection = new DragDeleteSelection(new BuildOperationHistory());
            var foundationA = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var foundationB = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(foundationA, out _));
            Assert.IsTrue(selection.TryAddTarget(foundationB, out _));
            Assert.IsFalse(selection.TryAddTarget(defaultTarget, out _));
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
            var selection = new DragDeleteSelection(new BuildOperationHistory());
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(foundation, out _));
            selection.CommitDelete();

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(defaultTarget, out _));
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
        }

        [Test]
        public void CategoryLockResetsAfterCancel()
        {
            // キャンセル後は次のセッションへカテゴリー固定が漏れない
            // The category lock does not leak into the next session after a cancel
            var selection = new DragDeleteSelection(new BuildOperationHistory());
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(foundation, out _));
            selection.CancelSelection();

            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(defaultTarget, out _));
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
        }
    }
}
