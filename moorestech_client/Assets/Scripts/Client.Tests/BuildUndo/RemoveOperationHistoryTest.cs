using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Tests.BuildUndo;
using Core.Master;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class RemoveOperationHistoryTest
    {
        [Test]
        public void CommitCapturesLineBeforeDeletionInvalidatesItsEndpoints()
        {
            var history = new BuildOperationHistory();
            var sender = new FakeRemovalRestoreSender();
            var selection = new DragDeleteSelection(history, sender);
            var tool = Guid.NewGuid();
            var target = new FakeDeleteTarget { Removable = true };
            target.RemovedObjects.Add(new RemovedConnectionLine(Vector3Int.zero, Vector3Int.right, tool, new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire)));

            // 確定で消えても先に記録した線を復元
            // Restore the captured line even if commit erases the target
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(target, out _));
            selection.CommitDelete();
            Assert.AreEqual(1, target.DeleteCount);
            Assert.IsEmpty(target.RemovedObjects);
            Assert.IsTrue(history.TryPop(out var record));
            record.UndoAsync(new NoBlockOccupancy()).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"wire:{Vector3Int.zero}-{Vector3Int.right}:{tool}" }, sender.Sent);
            Assert.IsFalse(history.TryPop(out _));
        }

        [Test]
        public void TargetsWithoutRestorableObjectsDoNotCreateHistory()
        {
            var history = new BuildOperationHistory();
            var selection = new DragDeleteSelection(history, new FakeRemovalRestoreSender());
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(new FakeDeleteTarget { Removable = true }, out _));
            selection.CommitDelete();
            Assert.IsFalse(history.TryPop(out _));
        }

        private sealed class NoBlockOccupancy : IBlockOccupancyQuery
        {
            public BlockFootprintOccupancy GetOccupancy(Vector3Int origin, BlockDirection direction, BlockId blockId)
            {
                Assert.Fail("A connection-only restore must not query block occupancy");
                return BlockFootprintOccupancy.Free;
            }
        }
    }
}
