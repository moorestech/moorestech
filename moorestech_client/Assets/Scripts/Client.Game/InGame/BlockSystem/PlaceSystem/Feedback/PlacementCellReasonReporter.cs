using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     カーソル下セルのローカル理由（地形干渉・既存ブロック重複）をこの順でツールチップ行に積む。通常設置・ベルト共用
    ///     Pushes the cursor cell's local reasons (terrain overlap, existing-block overlap) in that order; shared by normal and belt placement
    /// </summary>
    public static class PlacementCellReasonReporter
    {
        public static void Report(int cursorIndex, bool cursorOverlapsExistingBlock, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (groundOverlaps[cursorIndex]) feedback.AddBlockedByTerrain();
            if (cursorOverlapsExistingBlock) feedback.AddBlockedByExistingBlock();
        }
    }
}
