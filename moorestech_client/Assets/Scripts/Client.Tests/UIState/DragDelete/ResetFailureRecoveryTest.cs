using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class ResetFailureRecoveryTest
    {
        [Test]
        public void HoverChangeDoesNotRetryFailingReset()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { ThrowOnReset = true };
            var next = new FakeDeleteTarget();
            preview.Hover(broken);

            // 1回目の差し替えは例外になるが参照は手放されている
            // The first swap throws, but the reference has already been released
            Assert.Throws<InvalidOperationException>(() => preview.Hover(next));
            Assert.IsNull(preview.Current);

            // 次フレームの同じ差し替えは成功し、壊れた対象へ再び触れない
            // The same swap on the next frame succeeds without touching the broken target again
            preview.Hover(next);
            Assert.AreSame(next, preview.Current);
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(1, next.SetPreviewCount);
        }

        [Test]
        public void ReleaseAfterFailingResetIsNoOp()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { ThrowOnReset = true };
            preview.Hover(broken);

            // モード離脱の片付けは2回目で必ず通る
            // Mode-exit cleanup always passes on the second attempt
            Assert.Throws<InvalidOperationException>(() => preview.Release());
            Assert.DoesNotThrow(() => preview.Release());
            Assert.AreEqual(1, broken.ResetCount);
        }

        [Test]
        public void HoveringSameTargetDoesNotReapply()
        {
            var preview = new SingleHoverDeletePreview(new DeletePreviewResetRecovery());
            var target = new FakeDeleteTarget();
            preview.Hover(target);
            preview.Hover(target);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(0, target.ResetCount);
        }

        [Test]
        public void CancelSelectionClearsEvenWhenResetThrows()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(broken).IsAdded);

            // 例外が出ても選択は空になり、2回目のキャンセルは触れない
            // The selection is emptied despite the throw, and a second cancel touches nothing
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            Assert.IsFalse(selection.HasSelection());
            Assert.IsFalse(selection.HasSelection());
            Assert.DoesNotThrow(() => selection.CancelSelection());
            Assert.AreEqual(1, broken.ResetCount);
        }

        [Test]
        public void CommitDeleteDoesNotResendWhenResetThrows()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginStroke(DeleteSelectionIntent.Add);
            Assert.IsTrue(selection.TryAddTarget(broken).IsAdded);

            // 確定で例外が出ても選択は空になり、再確定で二重送信しない
            // A throw during commit still empties the selection, so a retry never resends
            Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            Assert.IsFalse(selection.HasSelection());
            selection.CommitDelete();
            Assert.AreEqual(1, broken.DeleteCount);
        }
        [TestCase(0)]
        [TestCase(1)]
        public void ResetFailureKeepsAllDeletesAndUndoHistory(int failingIndex)
        {
            var history = new BuildOperationHistory();
            var sender = new FakeRemovalRestoreSender();
            var selection = new DeleteSelection(history, sender, new DeletePreviewResetRecovery());
            var tool = Guid.NewGuid();
            var targets = new[] { new FakeDeleteTarget { Removable = true }, new FakeDeleteTarget { Removable = true } };
            targets[failingIndex].ThrowOnReset = true;
            selection.BeginStroke(DeleteSelectionIntent.Add);

            // 各対象に別の復元可能な線を記録する
            // Give each target a distinct restorable connection
            for (var index = 0; index < targets.Length; index++)
            {
                var position = Vector3Int.right * index;
                targets[index].RemovedObjects.Add(new RemovedConnectionLine(position, position + Vector3Int.up, tool,
                    new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire)));
                Assert.IsTrue(selection.TryAddTarget(targets[index]).IsAdded);
            }

            // 最初または途中の戻しが失敗しても全件送信済み
            // All deletes are sent even if the first or a later reset fails
            Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            Assert.IsFalse(selection.HasSelection());
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);
            Assert.DoesNotThrow(() => selection.CommitDelete());
            foreach (var target in targets) Assert.AreEqual(1, target.DeleteCount);
            Assert.AreEqual(1, targets[failingIndex].ResetCount);

            // 履歴は失敗前に積まれ、全件を一度だけ復元できる
            // History is pushed before the failure and restores every deletion once
            Assert.IsTrue(history.TryPop(out var record));
            record.UndoAsync(new NoBlockOccupancy()).GetAwaiter().GetResult();
            CollectionAssert.AreEquivalent(new[]
            {
                $"wire:{Vector3Int.zero}-{Vector3Int.up}:{tool}",
                $"wire:{Vector3Int.right}-{Vector3Int.right + Vector3Int.up}:{tool}"
            }, sender.Sent);
            Assert.IsFalse(history.TryPop(out _));
        }

        private sealed class NoBlockOccupancy : IBlockOccupancyQuery
        {
            public BlockFootprintOccupancy GetOccupancy(Vector3Int origin, BlockDirection direction, BlockId blockId)
            {
                Assert.Fail("Connection restore must not query block occupancy");
                return BlockFootprintOccupancy.Free;
            }
        }
    }
}
