using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using System;
using System.Reflection;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class SharedResetRecoveryTest
    {
        [Test]
        public void SuppressedHoverStillCommitsTheAimedTargetWithoutRetryingReset()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.SetHoverPreview(target);
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            // 表示参照を再保持しなくても、F確定は照準対象を使う
            // F confirmation uses the aimed target even when the visual reference is suppressed
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            var subject = selection.UpdateHoverPreview(target, out _);
            Assert.IsNull(selection.HoverPreview);
            Assert.IsTrue(selection.TryCommit(subject, out _));
            Assert.AreEqual(1, target.DeleteCount);
            Assert.AreEqual(1, target.ResetCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectionResetFailureCannotBeReacquiredBeforeNextExit(bool commit)
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender(), new Client.Game.InGame.UI.ProgressBar.ProgressBarState(), null, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.PlacementHeightOffset(), new Client.Game.InGame.UI.UIState.State.DragDelete.Box.DeleteBoxTargetResolver(null, null));
            var selection = (DeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteSelection).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
            var target = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);

            // キャンセルと確定のどちらの失敗もホバーと共有する
            // Share reset failures from both cancellation and commit with hover
            if (commit) Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            else Assert.Throws<InvalidOperationException>(() => service.ClearAll());
            Assert.IsFalse(selection.HasSelection());

            // 次の離脱入力前の同対象ホバーで再取得しない
            // Do not reacquire the same target before the next exit input
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            preview.Hover(target);
            preview.Hover(target);
            Assert.IsNull(preview.Current);
            Assert.DoesNotThrow(() => service.ClearAll());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(commit ? 1 : 0, target.DeleteCount);

            // 再ドラッグからも失敗済みResetを実行しない
            // A later drag must not repeat the failed reset either
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            Assert.DoesNotThrow(() => service.ClearAll());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void HoverResetFailureIsNotRetriedBySelectionCancel()
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender(), new Client.Game.InGame.UI.ProgressBar.ProgressBarState(), null, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.PlacementHeightOffset(), new Client.Game.InGame.UI.UIState.State.DragDelete.Box.DeleteBoxTargetResolver(null, null));
            var selection = (DeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteSelection).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
            var target = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            preview.Hover(target);
            Assert.Throws<InvalidOperationException>(() => service.ClearAll());

            selection.BeginStroke(DeleteSelectionIntent.Add);
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            Assert.DoesNotThrow(() => service.ClearAll());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void SuccessfulSelectionResetAllowsHoverAndDragAgain()
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender(), new Client.Game.InGame.UI.ProgressBar.ProgressBarState(), null, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.PlacementHeightOffset(), new Client.Game.InGame.UI.UIState.State.DragDelete.Box.DeleteBoxTargetResolver(null, null));
            var selection = (DeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteSelection).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            service.ClearAll();

            // 正常な選択解除後は単体ホバーも再ドラッグも通常どおり
            // Healthy selection resets allow both hover and another drag
            preview.Hover(target);
            Assert.AreSame(target, preview.Current);
            preview.Release();
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            service.ClearAll();
            Assert.AreEqual(3, target.ResetCount);
            Assert.AreEqual(3, target.SetPreviewCount);
        }
    }
}
