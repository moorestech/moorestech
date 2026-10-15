using System;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class PendingPreviewReleaseTest
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void NextServiceCancelDrainsUnstartedResetsWithoutResending(bool commit, bool deleteFails)
        {
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender());
            var selection = (DragDeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var first = new FakeDeleteTarget { Removable = true, ThrowOnDelete = deleteFails, ThrowOnReset = !deleteFails };
            var next = new FakeDeleteTarget { Removable = true };
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(first, out _));
            Assert.IsTrue(selection.TryAddTarget(next, out _));

            // 先頭の送信または復元が失敗しても、後続の解放を保持する
            // Retain later releases when the first send or reset throws
            if (commit) Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            else Assert.Throws<InvalidOperationException>(() => service.CancelSelection());
            Assert.IsFalse(selection.HasSelection());
            Assert.AreEqual(0, next.ResetCount);
            service.CancelSelection();
            service.CancelSelection();
            selection.CommitDelete();

            // 未着手分だけ一度解放し、失敗済み復元と送信は繰り返さない
            // Release unstarted previews once without repeating failed resets or sends
            Assert.AreEqual(1, first.ResetCount);
            Assert.AreEqual(1, next.ResetCount);
            Assert.AreEqual(commit ? 1 : 0, first.DeleteCount);
            Assert.AreEqual(commit && !deleteFails ? 1 : 0, next.DeleteCount);
        }

        [Test]
        public void LaterFailureKeepsRemainingQueueAcrossNewDrag()
        {
            var selection = new DragDeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var first = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var second = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var third = new FakeDeleteTarget { Removable = true };
            selection.BeginDrag();
            foreach (var target in new[] { first, second, third }) Assert.IsTrue(selection.TryAddTarget(target, out _));
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            selection.BeginDrag();
            var fourth = new FakeDeleteTarget { Removable = true };
            Assert.IsTrue(selection.TryAddTarget(fourth, out _));

            // 新ドラッグで保留キューを消さず、次の失敗でも後続を残す
            // Preserve queued releases across a new drag and another failure
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            Assert.AreEqual(0, third.ResetCount);
            selection.CancelSelection();
            foreach (var target in new[] { first, second, third, fourth })
            {
                Assert.AreEqual(1, target.ResetCount);
                Assert.AreEqual(0, target.DeleteCount);
            }
        }

        [Test]
        public void DestroyedPendingTargetDoesNotPreventLaterRelease()
        {
            var selection = new DragDeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var healthy = new FakeDeleteTarget { Removable = true };
            var gameObject = new GameObject("PendingDestroyedTarget");
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(broken, out _));
            Assert.IsTrue(selection.TryAddTarget(gameObject.AddComponent<DestroyableDeleteTarget>(), out _));
            Assert.IsTrue(selection.TryAddTarget(healthy, out _));
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            UnityEngine.Object.DestroyImmediate(gameObject);

            // 破棄済みの未着手対象は診断し、健全な後続を解放する
            // Diagnose the destroyed pending target and release the healthy successor
            LogAssert.Expect(LogType.Warning, "[DragDelete] selected target was destroyed before cancel");
            selection.CancelSelection();
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(1, healthy.ResetCount);
        }
    }
}
