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
        public void 最初の除外の赤戻しが失敗してもカテゴリー固定と論理解除を保つ()
        {
            var selection = Create();
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true, Category = "first" };
            var other = new FakeDeleteTarget { Removable = true, Category = "other" };
            selection.ApplyBoxTargets(new[] { broken, other }, DeleteSelectionIntent.Add);
            var revision = selection.Revision;
            selection.BeginStroke(DeleteSelectionIntent.Remove);

            // 赤戻しの失敗前に選択解除とカテゴリー固定が完了する
            // Deselect and lock the category before the preview reset can fail
            Assert.Throws<System.InvalidOperationException>(() => selection.UpdateStroke(DeleteAimResult.Found(broken)));
            Assert.IsFalse(selection.IsSelected(broken));
            Assert.AreNotEqual(revision, selection.Revision);
            Assert.IsTrue(selection.AimFilter.IsCategoryRequired);
            Assert.IsTrue(selection.AimFilter.Accepts(broken));
            selection.UpdateStroke(DeleteAimResult.Found(other));
            Assert.IsTrue(selection.IsSelected(other));
            Assert.AreEqual(1, broken.ResetCount);

            // 次のストロークは別カテゴリーを外せて失敗対象へ再接触しない
            // A new stroke can remove the other category without retrying the failed target
            selection.EndStroke();
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(other)));
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(0, selection.Count);
        }

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
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(removed)));
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
            selection.UpdateStroke(DeleteAimResult.Found(unselected));
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
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(line)));
            selection.UpdateStroke(DeleteAimResult.Found(block));
            Assert.IsTrue(selection.IsSelected(block), "a deselect stroke stays in the first category");
            selection.EndStroke();
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(block)));
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void 同じ論理キーの別ラッパーでも外せる()
        {
            var selection = Create();
            var block = new FakeDeleteTarget { Removable = true };
            selection.ApplyBoxTargets(new List<IDeleteTarget> { block }, DeleteSelectionIntent.Add);
            selection.BeginStroke(DeleteSelectionIntent.Remove);
            Assert.IsNull(selection.UpdateStroke(DeleteAimResult.Found(new FakeDeleteTarget { Removable = true, Key = block })));
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
            selection.UpdateStroke(DeleteAimResult.Found(target));
            Assert.IsFalse(selection.TryCancelSelection());
            Assert.AreEqual(1, target.ResetCount);
        }
    }
}
