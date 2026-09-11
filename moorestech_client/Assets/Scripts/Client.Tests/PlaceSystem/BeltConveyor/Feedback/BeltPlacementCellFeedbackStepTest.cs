using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor.Feedback
{
    /// <summary>
    ///     ベルト列の地形の扱いとカーソル解決規則がベルト側で決まることを検証
    ///     Verify that a belt run's terrain handling and cursor rule are decided on the belt side
    /// </summary>
    public class BeltPlacementCellFeedbackStepTest
    {
        [Test]
        public void 張替え列はカーソルが列外へ出ても理由が消えない()
        {
            // 張替えセルは既設の高さへ追従するのでYはカーソルと一致しない
            // Replace cells follow the existing heights, so their Y never matches the cursor
            var placeInfos = BuildReplaceRun();
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.ExistingBlock };
            var feedback = new PlacementFeedback();

            // カーソルが列のXZから外れた瞬間にツールチップが全部消えるのが直した実害
            // The fixed defect is the tooltip going blank the moment the cursor leaves the run's XZ
            var cursorIndex = BeltPlacementCellFeedbackStep.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(0, 0, 9), new List<bool> { false, false }, feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 張替え列はXZ一致でカーソル直下のセルを引く()
        {
            var placeInfos = BuildReplaceRun();
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.ExistingBlock, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            var cursorIndex = BeltPlacementCellFeedbackStep.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(0, 5, 0), new List<bool> { false, false }, feedback);

            Assert.AreEqual(0, cursorIndex);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 張替えセルは地形の重なりで不可にならず地形の理由も出ない()
        {
            // 張替えセルは既設ブロックの居るセルへ重ねるのが正常
            // A replace cell is meant to sit on an occupied cell
            var placeInfos = BuildReplaceRun();
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            BeltPlacementCellFeedbackStep.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(0, 0, 0), new List<bool> { true, true }, feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 張替えを含まないベルト列は完全一致と末尾フォールバックのまま()
        {
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0), Placeable = true },
                new() { Position = new Vector3Int(1, 0, 0), Placeable = true },
            };
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            // XZは先頭セルと一致するが、張替えを含まない列は末尾へ落ちる従来どおりの規則
            // The XZ matches the first cell, yet a run without replace cells keeps the old rule and falls back to the last cell
            var cursorIndex = BeltPlacementCellFeedbackStep.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, new Vector3Int(0, 5, 0), new List<bool> { true, false }, feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.IsFalse(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        private static List<PlaceInfo> BuildReplaceRun()
        {
            return new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0), IsReplace = true, Placeable = true },
                new() { Position = new Vector3Int(0, 1, 1), IsReplace = true, Placeable = true },
            };
        }
    }
}
