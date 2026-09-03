using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     BP設置不可理由を報告
    ///     Reports the BP paste block reason
    ///     全セル重複時のみ理由行
    ///     Only a full overlap gets a line
    /// </summary>
    public static class BlueprintPasteOverlapReasonReporter
    {
        public static void Report(IReadOnlyList<bool> placeableFlags, PlacementFeedback feedback)
        {
            if (0 < placeableFlags.Count && placeableFlags.All(flag => !flag)) feedback.AddBlockedByExistingBlock();
        }
    }
}
