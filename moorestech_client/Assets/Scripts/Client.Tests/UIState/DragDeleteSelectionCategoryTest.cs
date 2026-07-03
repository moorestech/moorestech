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
    }
}
