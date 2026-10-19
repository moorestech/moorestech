using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
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
            var service = new DeleteObjectService(new BuildOperationHistory(), new MouseCursorTooltipState(), new FakeRemovalRestoreSender(), new Client.Game.InGame.UI.ProgressBar.ProgressBarState(), null, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.PlacementHeightOffset(), new Client.Game.InGame.UI.UIState.State.DragDelete.Box.DeleteBoxTargetResolver(null, null));
            var selection = (DeleteSelection)typeof(DeleteObjectService).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            var first = new FakeDeleteTarget { Removable = true, ThrowOnDelete = deleteFails, ThrowOnReset = !deleteFails };
            var next = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(first).IsAdded);
            Assert.IsTrue(selection.TryAddTarget(next).IsAdded);

            // 送信失敗時だけ解放を次回へ残す
            // Drain after reset failure; retain releases only when sending fails
            if (commit) Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            else Assert.Throws<InvalidOperationException>(() => service.ClearAll());
            Assert.IsFalse(selection.HasSelection());
            Assert.AreEqual(deleteFails ? 0 : 1, next.ResetCount);
            service.ClearAll();
            service.ClearAll();
            selection.CommitDelete();

            // 未着手分だけ一度解放し、失敗済み復元と送信は繰り返さない
            // Release unstarted previews once without repeating failed resets or sends
            Assert.AreEqual(1, first.ResetCount);
            Assert.AreEqual(1, next.ResetCount);
            Assert.AreEqual(commit ? 1 : 0, first.DeleteCount);
            Assert.AreEqual(commit && !deleteFails ? 1 : 0, next.DeleteCount);
        }

        [Test]
        public void MultipleResetFailuresDrainAllTargetsBeforeNextDrag()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var first = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var second = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var third = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            foreach (var target in new[] { first, second, third }) Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            foreach (var target in new[] { first, second, third }) Assert.AreEqual(1, target.ResetCount);
            selection.BeginStroke(DeleteSelectionIntent.Add);
            var fourth = new FakeDeleteTarget { Removable = true };
            Assert.IsTrue(selection.TryAddTarget(fourth).IsAdded);

            // 新ドラッグに過去の失敗を持ち越さない
            // Do not carry old failures into cancellation of a new drag
            Assert.DoesNotThrow(() => selection.CancelSelection());
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
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            var healthy = new FakeDeleteTarget { Removable = true };
            var gameObject = new GameObject("PendingDestroyedTarget");
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(broken).IsAdded);
            Assert.IsTrue(selection.TryAddTarget(gameObject.AddComponent<DestroyableDeleteTarget>()).IsAdded);
            Assert.IsTrue(selection.TryAddTarget(healthy).IsAdded);
            UnityEngine.Object.DestroyImmediate(gameObject);

            // 破棄済みの未着手対象は診断し、健全な後続を解放する
            // Diagnose the destroyed pending target and release the healthy successor
            LogAssert.Expect(LogType.Warning, "[DragDelete] preview reset failed for FakeDeleteTarget; releasing remaining targets");
            LogAssert.Expect(LogType.Warning, "[DragDelete] selected target was destroyed before cancel");
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            Assert.DoesNotThrow(() => selection.CancelSelection());
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(1, healthy.ResetCount);
        }
    }
}
