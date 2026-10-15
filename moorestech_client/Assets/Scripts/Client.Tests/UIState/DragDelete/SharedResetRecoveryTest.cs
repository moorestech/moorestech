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
        [TestCase(false)]
        [TestCase(true)]
        public void SelectionResetFailureCannotBeReacquiredBeforeNextExit(bool commit)
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender());
            var selection = (DragDeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteObjectService).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var target = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));

            // キャンセルと確定のどちらの失敗もホバーと共有する
            // Share reset failures from both cancellation and commit with hover
            if (commit) Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            else Assert.Throws<InvalidOperationException>(() => service.CancelSelection());
            Assert.IsFalse(selection.HasSelection());

            // 次の離脱入力前の同対象ホバーで再取得しない
            // Do not reacquire the same target before the next exit input
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            preview.Hover(target);
            preview.Hover(target);
            Assert.IsNull(preview.Current);
            Assert.DoesNotThrow(() => service.CancelSelection());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(commit ? 1 : 0, target.DeleteCount);

            // 再ドラッグからも失敗済みResetを実行しない
            // A later drag must not repeat the failed reset either
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            Assert.DoesNotThrow(() => service.CancelSelection());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(1, target.SetPreviewCount);
        }

        [Test]
        public void HoverResetFailureIsNotRetriedBySelectionCancel()
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender());
            var selection = (DragDeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteObjectService).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var target = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            preview.Hover(target);
            Assert.Throws<InvalidOperationException>(() => service.CancelSelection());

            // ホバー由来の失敗も選択側で再実行しない
            // Selection must not retry a reset that already failed during hover
            selection.BeginDrag();
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            Assert.DoesNotThrow(() => service.CancelSelection());
            Assert.AreEqual(1, target.ResetCount);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void SuccessfulSelectionResetAllowsHoverAndDragAgain()
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender());
            var selection = (DragDeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var preview = (SingleHoverDeletePreview)typeof(DeleteObjectService).GetField("_hoverPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            service.CancelSelection();

            // 正常な選択解除後は単体ホバーも再ドラッグも通常どおり
            // Healthy selection resets allow both hover and another drag
            preview.Hover(target);
            Assert.AreSame(target, preview.Current);
            preview.Release();
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            service.CancelSelection();
            Assert.AreEqual(3, target.ResetCount);
            Assert.AreEqual(3, target.SetPreviewCount);
        }
    }
}
