using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    public static class BlueprintPasteFeedbackReporter
    {
        public static void Report(BlueprintPastePlan plan, PlacementFeedback feedback)
        {
            // 確定済みの共有判定からだけ理由行を作る
            // Build reason lines solely from the completed shared judgement
            if (0 < plan.Copies.Count && plan.CountCopies(BlueprintPasteCopyState.AllOverlapped) == plan.Copies.Count) feedback.AddBlockedByExistingBlock();
            if (0 < plan.CountCopies(BlueprintPasteCopyState.GroundNotFound)) feedback.AddGroundNotFound();
            if (0 < plan.ShortageRequirements.Count) feedback.AddMaterialShortages(ConstructionCostShortageCalculator.ToShortages(plan.ShortageRequirements));
        }
    }
}
