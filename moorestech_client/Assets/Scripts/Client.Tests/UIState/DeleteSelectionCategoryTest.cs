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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
            // 拒否理由も検証
            // Also verify the rejection reason
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
            // 土台同士は追加可能
            // Additional foundation targets are accepted
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
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
