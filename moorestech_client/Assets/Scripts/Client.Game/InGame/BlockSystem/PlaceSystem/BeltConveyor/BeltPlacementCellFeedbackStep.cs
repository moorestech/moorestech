using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    ///     ベルト列の地形の扱いとカーソル解決規則を決めてから共有Reporterへ渡す
    ///     Decides how a belt run treats terrain and which cursor rule it wants, then hands that to the shared reporter
    ///     張替えという語彙は共有層に持ち込まず、ここで具体的な入力（マスク済み地形列・解決規則）へ翻訳する
    ///     The replace vocabulary never reaches the shared layer; it is translated here into masked overlaps and a cursor rule
    /// </summary>
    public static class BeltPlacementCellFeedbackStep
    {
        public static int ApplyGroundOverlapsAndReport(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            // 張替えセルは既設ブロックの居るセルへ重ねるのが正常なので、地形の重なりを不可理由にしない
            // A replace cell is meant to sit on an occupied cell, so a terrain overlap is never a reason to block it
            var effectiveOverlaps = new List<bool>(groundOverlaps.Count);
            for (var i = 0; i < groundOverlaps.Count; i++) effectiveOverlaps.Add(groundOverlaps[i] && !placeInfos[i].IsReplace);

            // 張替えセルは既設の高さへ追従しカーソルのYと揃わないため、完全一致の次はXZで引く
            // Replace cells follow the existing heights and never match the cursor's Y, so XZ comes next after the exact match
            // XZも外れたら末尾へは落とさない。指していないセルの理由を出すより何も出さない方が正しい
            // When the XZ misses too it never falls back to the last cell; showing nothing beats showing a cell the cursor is not on
            var cursorMatch = placeInfos.Exists(info => info.IsReplace) ? PlacementCursorMatch.HorizontalOnly : PlacementCursorMatch.ExactCellOrLast;

            return PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(placeInfos, cellCauses, cursorCell, effectiveOverlaps, cursorMatch, feedback);
        }
    }
}
