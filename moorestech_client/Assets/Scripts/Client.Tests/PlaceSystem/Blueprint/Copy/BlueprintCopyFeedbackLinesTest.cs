using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopyFeedbackLinesTest
    {
        [Test]
        public void 範囲内が1以上なら件数行だけ()
        {
            var feedback = new PlacementFeedback();
            BlueprintCopyFeedbackLines.ReportBlocksInRange(3, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.BlueprintCopyBlocksInRange.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("3", feedback.Lines[0].TextParams[0]);
        }

        [Test]
        public void 範囲内が0なら空範囲の理由が増える()
        {
            var feedback = new PlacementFeedback();
            BlueprintCopyFeedbackLines.ReportBlocksInRange(0, feedback);
            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.BlueprintCopyEmptyRange.Key, feedback.Lines[1].Key.Key);
        }
    }
}
