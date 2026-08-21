using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     BP貼り付けの設置不可理由を報告する。部分重複は既存の除外送信のままとし、全セル重複のときのみ理由行を出す
    ///     Reports the BP paste block reason; partial overlap keeps the existing filtered send, only full overlap gets a line
    /// </summary>
    public static class BlueprintPasteOverlapReasonReporter
    {
        public static void Report(IReadOnlyList<bool> placeableFlags, PlacementFeedback feedback)
        {
            if (placeableFlags.Count > 0 && placeableFlags.All(flag => !flag)) feedback.AddBlockedByExistingBlock();
        }
    }
}
