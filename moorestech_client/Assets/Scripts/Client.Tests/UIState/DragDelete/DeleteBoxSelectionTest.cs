using System;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxSelectionTest
    {
        [Test]
        public void 始点を選ぶと終点待ちになりClearで戻る()
        {
            var selection = new DeleteBoxSelection();
            Assert.AreEqual(DeleteBoxPhase.Idle, selection.Phase);
            selection.SelectStart(new Vector3Int(1, 2, 3), DeleteSelectionIntent.Add);
            Assert.AreEqual(DeleteBoxPhase.SelectingEnd, selection.Phase);
            Assert.AreEqual(new Vector3Int(1, 2, 3), selection.StartCell);
            selection.Clear();
            Assert.AreEqual(DeleteBoxPhase.Idle, selection.Phase);
        }

        [Test]
        public void LastValidEndSurvivesMissingCursorUntilNextValidCell()
        {
            var selection = new DeleteBoxSelection();
            selection.SelectStart(new Vector3Int(2, 3, 4), DeleteSelectionIntent.Add);
            Assert.AreEqual(selection.StartCell, selection.EndCell);
            selection.SetEndCell(new Vector3Int(8, 9, 10));
            Assert.AreEqual(new Vector3Int(8, 9, 10), selection.EndCell);
            selection.Clear();
            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
            Assert.AreEqual(Vector3Int.zero, selection.EndCell);
        }

        [Test]
        public void 終点待ち中にもう一度始点は選べない()
        {
            var selection = new DeleteBoxSelection();
            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
            Assert.Throws<InvalidOperationException>(() => selection.SelectStart(Vector3Int.one, DeleteSelectionIntent.Add));
        }

        [Test]
        public void 始点で決めた除外の意図を終点まで保つ()
        {
            var selection = new DeleteBoxSelection();
            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Remove);
            selection.SetEndCell(Vector3Int.one);
            Assert.AreEqual(DeleteSelectionIntent.Remove, selection.Intent);
            selection.Clear();
            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
            Assert.AreEqual(DeleteSelectionIntent.Add, selection.Intent);
        }
    }
}
