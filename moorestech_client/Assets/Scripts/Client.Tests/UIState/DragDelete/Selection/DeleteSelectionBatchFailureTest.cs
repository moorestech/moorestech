using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete.Selection
{
    public class DeleteSelectionBatchFailureTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void 表示復元の失敗位置によらず箱の全対象が論理解除される(int failureIndex)
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var targets = new[] { new FakeDeleteTarget { Removable = true }, new FakeDeleteTarget { Removable = true }, new FakeDeleteTarget { Removable = true } };
            selection.ApplyBoxTargets(targets, DeleteSelectionIntent.Add);
            targets[failureIndex].ThrowOnReset = true;
            var revision = selection.Revision;

            // 失敗した表示以外も解除し、後続の削除へ持ち越さない
            // Release the other previews too and leave nothing for a subsequent delete
            Assert.Throws<InvalidOperationException>(() => selection.ApplyBoxTargets(targets, DeleteSelectionIntent.Remove));
            Assert.AreEqual(0, selection.Count);
            Assert.AreEqual(revision + targets.Length, selection.Revision);
            selection.CommitDelete();
            foreach (var target in targets)
            {
                Assert.AreEqual(1, target.ResetCount);
                Assert.AreEqual(0, target.DeleteCount);
            }
        }
    }
}
