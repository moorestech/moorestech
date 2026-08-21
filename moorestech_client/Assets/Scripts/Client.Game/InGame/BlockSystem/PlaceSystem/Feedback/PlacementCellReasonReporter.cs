using System.Collections.Generic;

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
    }
}
