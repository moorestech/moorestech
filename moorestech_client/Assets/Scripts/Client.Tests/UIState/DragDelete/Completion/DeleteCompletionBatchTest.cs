using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class DeleteCompletionBatchTest
    {
        [Test]
        public void SuccessAndRejectionProduceOnlySuccessfulUndoAfterAllResponses()
        {
            var history = new BuildOperationHistory();
            var sender = new FakeRemovalRestoreSender();
            var selection = new DeleteSelection(history, sender);
            var success = TargetWithLine(1);
            var rejected = TargetWithLine(2);
            selection.AddBoxTargets(new[] { success, rejected });
            selection.CommitDelete();
            Assert.IsFalse(history.TryPop(out _));
            rejected.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            Assert.IsFalse(history.TryPop(out _));
            success.Complete(DeleteCompletionKind.Succeeded, null);
            Assert.IsTrue(history.TryPop(out var record));
            record.UndoAsync(null).GetAwaiter().GetResult();
            Assert.AreEqual(1, sender.Sent.Count);
            StringAssert.Contains("(1, 0, 0)", sender.Sent[0]);
            Assert.IsFalse(history.TryPop(out _));
        }

        [Test]
        public void UnconfirmedCarDoesNotBlockRecordedSuccessAndLaterResultIsDelivered()
        {
            var history = new BuildOperationHistory();
            var selection = new DeleteSelection(history, new FakeRemovalRestoreSender());
            var recorded = TargetWithLine(1);
            var car = new FakeDeleteTarget { Removable = true, CompleteImmediately = false };
            var results = new List<DeleteCompletion>();
            selection.OnDeleteCompleted.Subscribe(results.Add);
            selection.AddBoxTargets(new[] { recorded, car });
            selection.CommitDelete();
            car.Complete(DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Tooltip.DeleteTrainQueueHeld);
            recorded.Complete(DeleteCompletionKind.Succeeded, null);
            Assert.IsTrue(history.TryPop(out _));
            car.Complete(DeleteCompletionKind.Succeeded, null);
            Assert.AreEqual(DeleteCompletionKind.Succeeded, results[^1].Kind);
            Assert.AreSame(car.GetDeleteTargetKey(), results[^1].TargetKey);
        }

        [Test]
        public void AllRejectedCreatesNoUndoAndReportsEveryTarget()
        {
            var history = new BuildOperationHistory();
            var selection = new DeleteSelection(history, new FakeRemovalRestoreSender());
            var target = TargetWithLine(1);
            var results = new List<DeleteCompletion>();
            selection.OnDeleteCompleted.Subscribe(results.Add);
            selection.AddBoxTargets(new[] { target });
            selection.CommitDelete();
            target.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            Assert.IsFalse(history.TryPop(out _));
            Assert.AreEqual(DeleteCompletionKind.Rejected, results[^1].Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.InventoryFull, results[^1].DenyReason);
        }

        [Test]
        public void SelectionChangePushResetsHoldBeforeTheNextAdvance()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var changes = new List<int>();
            selection.OnSelectionChanged.Subscribe(changes.Add);
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target);
            selection.TryAddTarget(target);
            Assert.AreEqual(1, changes.Count);
            selection.CancelSelection();
            Assert.AreEqual(2, changes.Count);
        }

        private static FakeDeleteTarget TargetWithLine(int x)
        {
            var target = new FakeDeleteTarget { Removable = true, CompleteImmediately = false };
            target.RemovedObjects.Add(new RemovedConnectionLine(new Vector3Int(x, 0, 0), new Vector3Int(x, 0, 2), Guid.NewGuid(), new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire)));
            return target;
        }
    }
}
