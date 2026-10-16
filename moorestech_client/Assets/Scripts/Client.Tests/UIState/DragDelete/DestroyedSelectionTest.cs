using System;
using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class DestroyedSelectionTest
    {
        private GameObject _selectedObject;

        [TearDown]
        public void TearDown()
        {
            if (_selectedObject != null) UnityEngine.Object.DestroyImmediate(_selectedObject);
        }

        [Test]
        public void DestroyedHoverIsReleasedAndLoggedOnce()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            _selectedObject = new GameObject("HoveredRail");
            preview.Hover(_selectedObject.AddComponent<DestroyableDeleteTarget>());
            UnityEngine.Object.DestroyImmediate(_selectedObject);

            LogAssert.Expect(LogType.Warning, "[DragDelete] hovered target was destroyed before reset");
            preview.Release();
            Assert.IsNull(preview.Current);
            Assert.DoesNotThrow(() => preview.Release());
        }

        [Test]
        public void DestroyedTargetIsSkippedBeforeUndoCapture()
        {
            var history = new BuildOperationHistory();
            var selection = new DeleteSelection(history, new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            _selectedObject = new GameObject("SelectedRail");
            var target = _selectedObject.AddComponent<DestroyableDeleteTarget>();
            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            UnityEngine.Object.DestroyImmediate(_selectedObject);

            // 破棄済みを記録へ渡さず理由を残す
            // Do not pass a destroyed target into capture; leave a diagnostic
            LogAssert.Expect(LogType.Warning, "[DragDelete] selected target was destroyed before commit");
            selection.CommitDelete();
            Assert.IsFalse(history.TryPop(out _));
        }

        [Test]
        public void DestroyedTargetIsLoggedDuringCancel()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            _selectedObject = new GameObject("SelectedRail");
            var target = _selectedObject.AddComponent<DestroyableDeleteTarget>();
            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            UnityEngine.Object.DestroyImmediate(_selectedObject);

            // キャンセルで失った対象も理由を記録する
            // Log a target lost before canceling the selection
            LogAssert.Expect(LogType.Warning, "[DragDelete] selected target was destroyed before cancel");
            selection.CancelSelection();
            Assert.IsFalse(selection.HasSelection());
        }
    }

    public class DestroyableDeleteTarget : MonoBehaviour, IDeleteTarget
    {
        public IObservable<DeleteCompletion> OnDeleteCompleted => Observable.Empty<DeleteCompletion>();
        public void SetRemovePreviewing() { }
        public void ResetMaterial() { }
        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            deniedReason = null;
            return true;
        }
        public void Delete() { }
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            Assert.Fail("Destroyed targets must never be captured");
        }
        public object GetPreviewResetKey() => this;
        public object GetDeleteTargetKey() { return this; }
        public string GetDestructionCategory() { return "default"; }
    }
}
