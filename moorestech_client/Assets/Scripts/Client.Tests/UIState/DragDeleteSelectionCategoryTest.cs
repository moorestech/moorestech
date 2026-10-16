using Client.Tests.BuildUndo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     破壊カテゴリーによる同時選択制限を検証するテスト
    ///     Tests verifying the destruction-category restriction on simultaneous selection
    /// </summary>
    public class DeleteSelectionCategoryTest
    {
        [Test]
        public void DefaultStartAllowsMultipleDefaultTargets()
        {
            // default開始ではdefault同士を複数選択できる
            // A default-started session can multi-select default targets
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var first = new FakeDeleteTarget { Removable = true, Category = "default" };
            var second = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(first).IsAdded);
            Assert.IsTrue(selection.TryAddTarget(second).IsAdded);
            selection.CommitDelete();

            Assert.AreEqual(1, first.DeleteCount);
            Assert.AreEqual(1, second.DeleteCount);
        }

        [Test]
        public void DefaultStartRejectsFoundationTarget()
        {
            // default開始では土台カテゴリーを追加選択できず、拒否理由が返る
            // A default-started session rejects a foundation target and returns the deny reason
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var start = new FakeDeleteTarget { Removable = true, Category = "default" };
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(start).IsAdded);
            var outcome = selection.TryAddTarget(foundation);
            Assert.IsFalse(outcome.IsAdded);
            var denyReason = outcome.DenyReason;
            Assert.IsTrue(denyReason.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.DifferentCategorySelection, denyReason.Value);
            selection.CommitDelete();

            Assert.AreEqual(1, start.DeleteCount);
            Assert.AreEqual(0, foundation.DeleteCount);
        }

        [Test]
        public void FoundationStartRejectsDefaultTarget()
        {
            // 土台開始では土台だけ選択でき、defaultは追加選択できない
            // A foundation-started session accepts only foundation, not default
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var foundationA = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var foundationB = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(foundationA).IsAdded);
            Assert.IsTrue(selection.TryAddTarget(foundationB).IsAdded);
            Assert.IsFalse(selection.TryAddTarget(defaultTarget).IsAdded);
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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(foundation).IsAdded);
            selection.CommitDelete();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(defaultTarget).IsAdded);
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
        }

        [Test]
        public void CategoryLockResetsAfterCancel()
        {
            // キャンセル後は次のセッションへカテゴリー固定が漏れない
            // The category lock does not leak into the next session after a cancel
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var foundation = new FakeDeleteTarget { Removable = true, Category = "foundation" };
            var defaultTarget = new FakeDeleteTarget { Removable = true, Category = "default" };

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(foundation).IsAdded);
            selection.CancelSelection();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);

            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(defaultTarget).IsAdded);
            selection.CommitDelete();

            Assert.AreEqual(1, defaultTarget.DeleteCount);
        }

        [Test]
        public void AimFilterIsFixedByFirstTargetAndResetOnNewDrag()
        {
            // 最初の対象で固定、新ドラッグで解除
            // The first target fixes the filter; a new drag resets it
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            selection.BeginStroke();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);

            var line = new FakeDeleteTarget { Removable = true, Category = "connectionLine" };
            selection.TryAddTarget(line);
            Assert.IsTrue(selection.AimFilter.IsCategoryRequired);
            Assert.IsTrue(selection.AimFilter.Accepts(line));
            Assert.IsFalse(selection.AimFilter.Accepts(new FakeDeleteTarget { Category = "default" }));

            selection.BeginStroke();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);
        }
    }
}
