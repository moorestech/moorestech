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
            if (_selectedObject != null) Object.DestroyImmediate(_selectedObject);
        }

        [Test]
        public void DestroyedTargetIsSkippedBeforeUndoCapture()
        {
            var history = new BuildOperationHistory();
            var selection = new DragDeleteSelection(history, new FakeRemovalRestoreSender());
            _selectedObject = new GameObject("SelectedRail");
            var target = _selectedObject.AddComponent<DestroyableDeleteTarget>();
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            Object.DestroyImmediate(_selectedObject);

            // 破棄済みを記録へ渡さず理由を残す
            // Do not pass a destroyed target into capture; leave a diagnostic
            LogAssert.Expect(LogType.Warning, "[DragDelete] selected target was destroyed before commit");
            selection.CommitDelete();
            Assert.IsFalse(history.TryPop(out _));
        }
    }

    public class DestroyableDeleteTarget : MonoBehaviour, IDeleteTarget
    {
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
        public object GetDeleteTargetKey() { return this; }
        public string GetDestructionCategory() { return "default"; }
    }
}
