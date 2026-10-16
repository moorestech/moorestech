using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteSelectionPreviewTest
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void 同じキーの別ラッパーを選んでも旧ホバー要求が残らない(bool box, bool commit)
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var key = new object();
            var hovered = new FakeDeleteTarget { Key = key, Removable = true };
            var selected = new FakeDeleteTarget { Key = key, Removable = true };

            // 箱となぞりの両方で実際の要求者を引き継ぐ
            // Transfer the actual requester for both box and stroke selection
            selection.SetHoverPreview(hovered);
            if (box) selection.AddBoxTargets(new[] { selected });
            else
            {
                selection.BeginStroke();
                selection.TryAddTarget(selected);
            }
            Assert.AreEqual(1, hovered.ResetCount);
            Assert.IsNull(selection.HoverPreview);
            selection.SetHoverPreview(null);
            if (commit) selection.CommitDelete();
            else selection.CancelSelection();

            Assert.AreEqual(hovered.SetPreviewCount, hovered.ResetCount);
            Assert.AreEqual(selected.SetPreviewCount, selected.ResetCount);
            Assert.AreEqual(0, selection.Count);
        }
    }
}
