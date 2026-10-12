using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopySelectionTest
    {
        [Test]
        public void 始点_終点_名前入力_キャンセルで終点選択へ戻る()
        {
            var selection = new BlueprintCopySelection();
            Assert.AreEqual(BlueprintCopyPhase.SelectingStart, selection.Phase);

            selection.SelectStart(new Vector3Int(4, 32, 4));
            selection.SelectEnd(new Vector3Int(0, 33, 0));
            Assert.AreEqual(BlueprintCopyPhase.AwaitingName, selection.Phase);

            selection.ReturnToEndSelection();
            Assert.AreEqual(BlueprintCopyPhase.SelectingEnd, selection.Phase);
            Assert.AreEqual(new Vector3Int(4, 32, 4), selection.StartCell);

            selection.SelectEnd(new Vector3Int(0, 33, 0));
            selection.BeginCreate();
            Assert.AreEqual(BlueprintCopyPhase.Creating, selection.Phase);
            selection.Clear();
            Assert.AreEqual(BlueprintCopyPhase.SelectingStart, selection.Phase);
        }

        [Test]
        public void 範囲は2セルの成分ごとのmin_max()
        {
            var (min, max) = BlueprintCopySelection.CalcBox(new Vector3Int(4, 32, 0), new Vector3Int(0, 33, 4));
            Assert.AreEqual(new Vector3Int(0, 32, 0), min);
            Assert.AreEqual(new Vector3Int(4, 33, 4), max);
        }

        [Test]
        public void 局面外の遷移は例外()
        {
            var selection = new BlueprintCopySelection();
            Assert.Throws<InvalidOperationException>(() => selection.SelectEnd(Vector3Int.zero));
            Assert.Throws<InvalidOperationException>(() => selection.ReturnToEndSelection());
            Assert.Throws<InvalidOperationException>(() => selection.BeginCreate());
        }
    }
}
