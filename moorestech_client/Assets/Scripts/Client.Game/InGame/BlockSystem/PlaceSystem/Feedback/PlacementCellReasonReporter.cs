using System;
using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     全設置系共通の埋まり反映入口（ADR 0081）
    ///     The shared entry for every placement system that applies per-cell terrain sinks to Placeable and reason lines (ADR 0081)
    ///     設置系固有の理由は積まない。共有行の後にその系自身が積む
    ///     System-specific reasons are not pushed here; each system pushes its own after these shared lines
    /// </summary>
    public static class PlacementCellReasonReporter
    {
        // cursorIndexはPlacementCursorCellResolverがterrainSinksと同じplaceInfos列から解決した添字であること（呼び出し側の不変条件）
        // cursorIndex must come from PlacementCursorCellResolver over the same placeInfos list that produced terrainSinks (caller's invariant)
        internal static void Report(int cursorIndex, PlacementBlockCause cursorCause, IReadOnlyList<bool> terrainSinks, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (terrainSinks[cursorIndex]) feedback.AddBlockedByTerrain();

            ReportCause(cursorCause, feedback);
        }

        // 地形以外の共有原因を積む
        // Pushes the shared causes other than terrain sinks
        internal static void ReportCause(PlacementBlockCause cursorCause, PlacementFeedback feedback)
        {
            // 原因を取り違えると空セルに「埋まっています」と誤案内するため、原因ごとに文言を分ける
            // Confusing the causes would mis-report "occupied" on an empty cell, so each cause gets its own wording
            switch (cursorCause)
            {
                case PlacementBlockCause.None:
                    break;
                case PlacementBlockCause.ExistingBlock:
                    feedback.AddBlockedByExistingBlock();
                    break;
                case PlacementBlockCause.GroundNotFound:
                    feedback.AddGroundNotFound();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cursorCause), cursorCause, null);
            }
        }

        // 解決→埋まり反映→理由集約を1回で行う
        // Resolves the cursor cell, applies terrain sinks, and reports reasons in one call (the shared shape normal/belt each rebuilt)
        // cellCausesはplaceInfosと同じ添字で並走するセル毎の共有原因列（呼び出し側が判定して渡す）
        // cellCauses is the per-cell shared cause column indexed like placeInfos, filled in by the caller that judged them
        // 戻り値は解決済みcursorIndex（呼び出し側が後段の処理でカーソルセルを再解決せず使い回すため）
        // Returns the resolved cursorIndex so the caller can reuse it in later steps instead of re-resolving
        public static int ApplyTerrainSinksAndReport(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, IReadOnlyList<bool> terrainSinks, PlacementFeedback feedback)
        {
            var cursorIndex = PlacementCursorCellResolver.Resolve(placeInfos, cursorCell);
            var cursorCause = 0 <= cursorIndex ? cellCauses[cursorIndex] : PlacementBlockCause.None;

            for (var i = 0; i < terrainSinks.Count; i++)
            {
                if (terrainSinks[i]) placeInfos[i].Placeable = false;
            }

            Report(cursorIndex, cursorCause, terrainSinks, feedback);
            return cursorIndex;
        }
    }
}
