using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class LateRemovalUndoTest
    {
        [TestCase(true)] [TestCase(false)]
        public void UnknownKeepsBatchReservedUntilLateResult(bool success)
        {
            var history = new BuildOperationHistory();
            var sender = new FakeRemovalRestoreSender();
            var selection = new DeleteSelection(history, sender, new DeletePreviewResetRecovery());
            var immediate = Target(1);
            var late = Target(2);
            selection.ApplyBoxTargets(new[] { immediate, late }, DeleteSelectionIntent.Add);
            selection.CommitDelete();
            immediate.Complete(DeleteCompletionKind.Succeeded, null);
            late.Complete(DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteUndoAwaitingResult);
            // 未確認のバッチはUndo消費前に固定し、再送も防ぐ
            // Keep the unknown batch unconsumable and prevent duplicate sends
            Assert.IsFalse(history.TryPop(out _));
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.DeleteRequestPending, selection.TryAddTarget(late).DenyReason);
            Assert.AreEqual(DeleteHoldSubjectKind.None, selection.UpdateHoverPreview(late, out var reason).Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.DeleteRequestPending, reason);
            Assert.AreEqual(0, selection.ApplyBoxTargets(new[] { late }, DeleteSelectionIntent.Add));
            Assert.AreEqual(1, late.DeleteCount);

            // 確定後にだけ同じ履歴枠を完成し、成功分を一度だけ戻す
            // Complete the original history slot only on final results and restore successes once
            late.Complete(success ? DeleteCompletionKind.Succeeded : DeleteCompletionKind.Rejected, null);
            Assert.IsTrue(history.TryPop(out var record));
            record.UndoAsync(null).GetAwaiter().GetResult();
            Assert.AreEqual(success ? 2 : 1, sender.Sent.Count);
            Assert.IsFalse(history.TryPop(out _));
            if (!success)
            {
                selection.BeginStroke(DeleteSelectionIntent.Add);
                Assert.IsTrue(selection.TryAddTarget(late).IsAdded);
            }
        }

        [Test]
        public void LaterBatchUndoDoesNotConsumeEarlierUnknownReservation()
        {
            var history = new BuildOperationHistory();
            var sender = new FakeRemovalRestoreSender();
            var selection = new DeleteSelection(history, sender, new DeletePreviewResetRecovery());
            var first = Target(1);
            selection.ApplyBoxTargets(new[] { first }, DeleteSelectionIntent.Add);
            selection.CommitDelete();
            first.Complete(DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteUndoAwaitingResult);
            var second = Target(2);
            selection.ApplyBoxTargets(new[] { second }, DeleteSelectionIntent.Add);
            selection.CommitDelete();
            second.Complete(DeleteCompletionKind.Succeeded, null);
            Assert.IsTrue(history.TryPop(out var newer));
            newer.UndoAsync(null).GetAwaiter().GetResult();
            StringAssert.Contains("(2, 0, 0)", sender.Sent[0]);
            Assert.IsFalse(history.TryPop(out _));
            first.Complete(DeleteCompletionKind.Succeeded, null);
            Assert.IsTrue(history.TryPop(out var older));
            older.UndoAsync(null).GetAwaiter().GetResult();
            StringAssert.Contains("(1, 0, 0)", sender.Sent[1]);
            Assert.IsFalse(history.TryPop(out _));
        }

        private static FakeDeleteTarget Target(int x)
        {
            var target = new FakeDeleteTarget { Removable = true, CompleteImmediately = false };
            target.RemovedObjects.Add(new RemovedConnectionLine(new Vector3Int(x, 0, 0), new Vector3Int(x, 0, 2), Guid.NewGuid(), new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire)));
            return target;
        }
    }
}
