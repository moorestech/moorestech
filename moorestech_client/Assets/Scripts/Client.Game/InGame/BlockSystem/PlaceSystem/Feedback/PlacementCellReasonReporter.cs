using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     カーソル下セルのローカル理由（地形干渉・既存ブロック重複）をこの順でツールチップ行に積む。通常設置・ベルト共用
    ///     Pushes the cursor cell's local reasons (terrain overlap, existing-block overlap) in that order; shared by normal and belt placement
    /// </summary>
    public static class PlacementCellReasonReporter
    {
        // cursorIndexはPlacementCursorCellResolverがgroundOverlapsと同じplaceInfos列から解決した添字であること（呼び出し側の不変条件）
        // cursorIndex must come from PlacementCursorCellResolver over the same placeInfos list that produced groundOverlaps (caller's invariant)
        public static void Report(int cursorIndex, bool cursorOverlapsExistingBlock, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (groundOverlaps[cursorIndex]) feedback.AddBlockedByTerrain();
            if (cursorOverlapsExistingBlock) feedback.AddBlockedByExistingBlock();
        }

        // カーソルセル解決→地面接触の反映→理由集約を1回で行う（通常設置・ベルトが個別に組んでいた同一手順の集約先）
        // Resolves the cursor cell, applies ground overlaps, and reports reasons in one call (the shared shape normal/belt each rebuilt)
        // 戻り値は解決済みcursorIndex（呼び出し側が後段の処理でカーソルセルを再解決せず使い回すため）
        // Returns the resolved cursorIndex so the caller can reuse it in later steps instead of re-resolving
        public static int ApplyGroundOverlapsAndReport(List<PlaceInfo> placeInfos, Vector3Int cursorCell, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            var cursorIndex = PlacementCursorCellResolver.Resolve(placeInfos, cursorCell);
            var cursorOverlapsExistingBlock = 0 <= cursorIndex && !placeInfos[cursorIndex].Placeable;

            for (var i = 0; i < groundOverlaps.Count; i++)
            {
                if (groundOverlaps[i]) placeInfos[i].Placeable = false;
            }

            Report(cursorIndex, cursorOverlapsExistingBlock, groundOverlaps, feedback);
            return cursorIndex;
        }
    }
}
