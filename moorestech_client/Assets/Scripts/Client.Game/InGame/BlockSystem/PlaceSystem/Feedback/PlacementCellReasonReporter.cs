using System;
using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     カーソル下セルの共有理由（地形干渉 → 共有の設置不可原因）をこの順でツールチップ行に積む。設置系で共用
    ///     Pushes the cursor cell's shared reasons (terrain overlap, then the shared block cause) in that order; shared by every placement system
    ///     設置系固有の理由は積まない。共有行の後にその系自身が積む
    ///     System-specific reasons are not pushed here; each system pushes its own after these shared lines
    /// </summary>
    public static class PlacementCellReasonReporter
    {
        // cursorIndexはPlacementCursorCellResolverがgroundOverlapsと同じplaceInfos列から解決した添字であること（呼び出し側の不変条件）
        // cursorIndex must come from PlacementCursorCellResolver over the same placeInfos list that produced groundOverlaps (caller's invariant)
        internal static void Report(int cursorIndex, PlacementBlockCause cursorCause, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (groundOverlaps[cursorIndex]) feedback.AddBlockedByTerrain();

            // 原因を取り違えると空セルに「埋まっています」と誤案内するため、原因ごとに文言を分ける
            // Confusing the causes would mis-report "occupied" on an empty cell, so each cause gets its own wording
            switch (cursorCause)
            {
                case PlacementBlockCause.None:
                    break;
                case PlacementBlockCause.ExistingBlock:
                    feedback.AddBlockedByExistingBlock();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cursorCause), cursorCause, null);
            }
        }

        // カーソルセル解決→地面接触の反映→理由集約を1回で行う（通常設置・ベルトが個別に組んでいた同一手順の集約先）
        // Resolves the cursor cell, applies ground overlaps, and reports reasons in one call (the shared shape normal/belt each rebuilt)
        // cellCausesはplaceInfosと同じ添字で並走するセル毎の共有原因列（呼び出し側が判定して渡す）
        // cellCauses is the per-cell shared cause column indexed like placeInfos, filled in by the caller that judged them
        // 戻り値は解決済みcursorIndex（呼び出し側が後段の処理でカーソルセルを再解決せず使い回すため）
        // Returns the resolved cursorIndex so the caller can reuse it in later steps instead of re-resolving
        public static int ApplyGroundOverlapsAndReport(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            var cursorIndex = PlacementCursorCellResolver.Resolve(placeInfos, cursorCell);
            var cursorCause = 0 <= cursorIndex ? cellCauses[cursorIndex] : PlacementBlockCause.None;

            for (var i = 0; i < groundOverlaps.Count; i++)
            {
                if (groundOverlaps[i]) placeInfos[i].Placeable = false;
            }

            Report(cursorIndex, cursorCause, groundOverlaps, feedback);
            return cursorIndex;
        }
    }
}
