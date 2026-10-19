using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete.Selection
{
    public class DeleteSelectionDeselectTest
    {
        private static DeleteSelection Create() => new(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());

        [Test]
        public void ストローク更新は開始時に固定した意図で追加と除外を行う()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(target)));
            Assert.AreEqual(1, selection.Count);
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(target)));
            Assert.AreEqual(0, selection.Count);
            Assert.AreEqual(1, target.ResetCount);
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.UpdateStroke(DeleteAimResult.Found(target));
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void 除外のなぞりは選択済みを外して赤を戻す()
        {
            var selection = Create();
            var kept = new FakeDeleteTarget { Removable = true };
            var removed = new FakeDeleteTarget { Removable = true };
            selection.ApplyBoxTargets(new List<IDeleteTarget> { kept, removed }, DeleteSelectionIntent.Add);
            var revision = selection.Revision;
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsTrue(selection.TryRemoveStrokeTarget(removed));
            Assert.AreEqual(1, selection.Count);
            Assert.AreEqual(1, removed.ResetCount);
            Assert.AreEqual(0, kept.ResetCount);
            Assert.AreNotEqual(revision, selection.Revision);
            selection.CommitDelete();
            Assert.AreEqual(1, kept.DeleteCount);
            Assert.AreEqual(0, removed.DeleteCount);
        }

        [Test]
        public void 未選択の対象は外さず版も進めない()
        {
            var selection = Create();
            selection.ApplyBoxTargets(new List<IDeleteTarget> { new FakeDeleteTarget { Removable = true } }, DeleteSelectionIntent.Add);
            var unselected = new FakeDeleteTarget { Removable = true };
            var revision = selection.Revision;
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsFalse(selection.TryRemoveStrokeTarget(unselected));
            Assert.AreEqual(revision, selection.Revision);
            Assert.AreEqual(0, unselected.ResetCount);
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void 除外のなぞりは最初に外したカテゴリーに固定する()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            selection.TryAddTarget(line);
            selection.ApplyBoxTargets(new List<IDeleteTarget> { block }, DeleteSelectionIntent.Add);
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsTrue(selection.TryRemoveStrokeTarget(line));
            Assert.IsFalse(selection.TryRemoveStrokeTarget(block), "a deselect stroke stays in the first category");
            selection.EndStroke();
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsTrue(selection.TryRemoveStrokeTarget(block));
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void 同じ論理キーの別ラッパーでも外せる()
        {
            var selection = Create();
            var block = new FakeDeleteTarget { Removable = true };
            selection.ApplyBoxTargets(new List<IDeleteTarget> { block }, DeleteSelectionIntent.Add);
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsTrue(selection.TryRemoveStrokeTarget(new FakeDeleteTarget { Removable = true, Key = block }));
            Assert.AreEqual(1, block.ResetCount, "the selected instance is un-redded");
        }

        [Test]
        public void 箱の除外は選択済みだけを外し件数を返す()
        {
            var selection = Create();
            var a = new FakeDeleteTarget { Removable = true };
            var b = new FakeDeleteTarget { Removable = true };
            var c = new FakeDeleteTarget { Removable = true };
            selection.ApplyBoxTargets(new List<IDeleteTarget> { a, b, c }, DeleteSelectionIntent.Add);
            Assert.AreEqual(2, selection.ApplyBoxTargets(new List<IDeleteTarget> { a, b, new FakeDeleteTarget { Removable = true } }, DeleteSelectionIntent.Remove));
            Assert.AreEqual(1, selection.Count);
            Assert.IsTrue(selection.IsSelected(c));
            Assert.IsFalse(selection.IsSelected(a));
        }

        [Test]
        public void 通常のなぞりは除外を引き継がない()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            selection.EndStroke();
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void 全件外した後の全解除は外した対象の赤を二重に戻さない()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.ApplyBoxTargets(new List<IDeleteTarget> { target }, DeleteSelectionIntent.Add);
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            selection.TryRemoveStrokeTarget(target);
            Assert.IsFalse(selection.TryCancelSelection());
            Assert.AreEqual(1, target.ResetCount);
        }
    }
}
