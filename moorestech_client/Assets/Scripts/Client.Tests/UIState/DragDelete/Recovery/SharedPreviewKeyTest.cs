using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class SharedPreviewKeyTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OtherWrapperCannotRetryFailedSharedView(bool commit)
        {
            var recovery = new DeletePreviewResetRecovery();
            var hover = new SingleHoverDeletePreview(recovery);
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), recovery);
            var view = new object();
            var first = new FakeDeleteTarget { PreviewResetKey = view, ThrowOnReset = true };
            var other = new FakeDeleteTarget { PreviewResetKey = view, Removable = true, ThrowOnReset = true };
            hover.Hover(first);
            Assert.Throws<InvalidOperationException>(() => hover.Release());

            // 別ラッパーも同じ復元先なので警告と抑止を共有する
            // Different wrappers of one view share suppression and its single warning
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            hover.Hover(other);
            hover.Hover(first);
            Assert.IsNull(hover.Current);
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(other).IsAdded);
            if (commit) selection.CommitDelete();
            else selection.CancelSelection();
            Assert.AreEqual(0, other.ResetCount);
            Assert.AreEqual(0, other.SetPreviewCount);
            Assert.AreEqual(commit ? 1 : 0, other.DeleteCount);
            Assert.AreEqual(1, first.ResetCount);
        }

        [Test]
        public void SelectionFailureSuppressesOtherHoverWrapper()
        {
            var recovery = new DeletePreviewResetRecovery();
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), recovery);
            var view = new object();
            var selected = new FakeDeleteTarget { PreviewResetKey = view, Removable = true, ThrowOnReset = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(selected).IsAdded);
            Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());

            // 選択から単体ホバーへ渡る方向でも同一表示の失敗を共有する
            // Share failure identity from selection back to single hover as well
            var hover = new SingleHoverDeletePreview(recovery);
            var other = new FakeDeleteTarget { PreviewResetKey = view };
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            hover.Hover(other);
            hover.Release();
            Assert.IsNull(hover.Current);
            Assert.AreEqual(0, other.SetPreviewCount);
            Assert.AreEqual(0, other.ResetCount);
        }
    }
}
