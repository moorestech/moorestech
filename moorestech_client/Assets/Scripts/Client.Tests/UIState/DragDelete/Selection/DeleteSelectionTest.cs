using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using System.Collections.Generic;
using Client.Tests.BuildUndo;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class DeleteSelectionTest
    {
        private static DeleteSelection Create() => new(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());

        [Test]
        public void AddRemovableTargetAddsAndPreviews()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.AreEqual(DeleteAddKind.Added, selection.TryAddTarget(target).Kind);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void AddDifferentWrappersWithSameKeyReportsAlreadySelected()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(target);
            var revision = selection.Revision;
            var wrapper = new FakeDeleteTarget { Removable = true, Key = target };
            Assert.AreEqual(DeleteAddKind.AlreadySelected, selection.TryAddTarget(wrapper).Kind);
            Assert.AreEqual(revision, selection.Revision);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(1, selection.Count);
            selection.CommitDelete();
            Assert.AreEqual(1, target.DeleteCount + wrapper.DeleteCount);
        }

        [Test]
        public void AddNonRemovableTargetReturnsReason()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = false, DenyReason = LocalizationKeys.Ui.Delete.StationInternalRail };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            var outcome = selection.TryAddTarget(target);
            selection.CommitDelete();
            Assert.AreEqual(DeleteAddKind.NotRemovable, outcome.Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.StationInternalRail, outcome.DenyReason);
            Assert.AreEqual(0, target.SetPreviewCount);
            Assert.AreEqual(0, target.DeleteCount);
        }

        [Test]
        public void CancelSelectionResetsAndKillsStroke()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(target);
            selection.CancelSelection();
            Assert.AreEqual(1, target.ResetCount);
            Assert.IsFalse(selection.HasSelection());
            Assert.AreEqual(DeleteAddKind.StrokeDead, selection.TryAddTarget(target).Kind);
        }

        [Test]
        public void CommitDeleteDeletesTargetThenClears()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(target);
            selection.CommitDelete();
            selection.CommitDelete();
            Assert.AreEqual(1, target.DeleteCount);
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void BeginStrokeAfterCancelRevivesStroke()
        {
            var selection = Create();
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(new FakeDeleteTarget { Removable = true });
            selection.CancelSelection();
            selection.BeginStroke(DeleteSelectionIntent.Add);
        }

        [Test]
        public void EndStrokeKeepsSelectionAndResetsCategory()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(line);
            var mismatch = selection.TryAddTarget(block);
            Assert.AreEqual(DeleteAddKind.CategoryMismatch, mismatch.Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.DifferentCategorySelection, mismatch.DenyReason);
            Assert.IsTrue(selection.AimFilter.IsCategoryRequired);
            selection.EndStroke();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(block).IsAdded);
            Assert.AreEqual(2, selection.Count);
        }

        [Test]
        public void AddBoxTargetsIgnoresCategoryAndDedupes()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(line);
            var block = new FakeDeleteTarget { Removable = true };
            var wrapper = new FakeDeleteTarget { Removable = true, Key = block };
            var added = selection.ApplyBoxTargets(new List<IDeleteTarget> { block, wrapper, line }, DeleteSelectionIntent.Add);
            Assert.AreEqual(1, added);
            Assert.AreEqual(2, selection.Count);
            Assert.AreEqual(1, block.SetPreviewCount);
            var revision = selection.Revision;
            Assert.AreEqual(0, selection.ApplyBoxTargets(new[] { wrapper }, DeleteSelectionIntent.Add));
            Assert.AreEqual(revision, selection.Revision);
            selection.CommitDelete();
            Assert.AreEqual(1, block.DeleteCount + wrapper.DeleteCount);
        }

        [Test]
        public void HoverPreviewSwapsRedButKeepsSelectedRed()
        {
            var selection = Create();
            var selected = new FakeDeleteTarget { Removable = true };
            var other = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(selected);
            selection.SetHoverPreview(selected);
            selection.SetHoverPreview(other);
            Assert.AreEqual(0, selected.ResetCount, "a selected target keeps its red when the hover leaves it");
            Assert.AreEqual(1, other.SetPreviewCount);
            selection.SetHoverPreview(null);
            Assert.AreEqual(1, other.ResetCount);
            Assert.IsNull(selection.HoverPreview);
        }

        [Test]
        public void CancelAndCommitAlsoResetHoverPreview()
        {
            var selection = Create();
            var hovered = new FakeDeleteTarget { Removable = true };
            selection.SetHoverPreview(hovered);
            selection.CancelSelection();
            Assert.AreEqual(1, hovered.ResetCount);
            Assert.IsNull(selection.HoverPreview);
            selection.SetHoverPreview(hovered);
            selection.CommitDelete();
            Assert.AreEqual(2, hovered.ResetCount);
        }

        [Test]
        public void RevisionAdvancesOnAddCancelAndCommit()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            var r0 = selection.Revision;
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(target);
            var r1 = selection.Revision;
            selection.TryAddTarget(target);
            Assert.AreEqual(r1, selection.Revision, "duplicate add must not advance the revision");
            selection.CommitDelete();
            var r2 = selection.Revision;
            selection.CancelSelection();
            Assert.Greater(r1, r0);
            Assert.Greater(r2, r1);
            Assert.Greater(selection.Revision, r2);
        }
    }
}
