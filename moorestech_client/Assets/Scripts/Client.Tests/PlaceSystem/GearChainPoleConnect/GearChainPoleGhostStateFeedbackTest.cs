using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    // ゴースト状態ごとの理由行を検証する
    // Verify the reason line for each ghost state
    public class GearChainPoleGhostStateFeedbackTest
    {
        [Test]
        public void GhostGroundNotFoundReportsGroundNotFoundLineTest()
        {
            // 地表が取れずゴーストが無いときは地表なしの行だけ返す
            // With no ghost because the footprint has no ground, only the ground-not-found line is returned
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.GhostState = GearChainPoleGhostState.GroundNotFound;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, result.FeedbackLines[0].Key.Key);
        }
    }
}
