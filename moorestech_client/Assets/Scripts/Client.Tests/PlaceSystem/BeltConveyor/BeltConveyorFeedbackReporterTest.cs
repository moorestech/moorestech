using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルトのツールチップ行の積み方のテスト（ADR 0081 決定7）
    /// Tests for how belt tooltip lines are pushed (ADR 0081 decision 7)
    /// </summary>
    public class BeltConveyorFeedbackReporterTest
    {
        [Test]
        public void 断念したときは固有理由が無くても案内を1行出す()
        {
            var feedback = new PlacementFeedback();

            BeltConveyorFeedbackReporter.Report(feedback, BeltConveyorPlacementBlockReason.None, true);

            Assert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned.Key }, feedback.Lines.Select(line => line.Key.Key).ToArray());
        }

        [Test]
        public void 断念していなければ案内は出さない()
        {
            var feedback = new PlacementFeedback();

            BeltConveyorFeedbackReporter.Report(feedback, BeltConveyorPlacementBlockReason.None, false);

            Assert.AreEqual(0, feedback.Lines.Count);
        }

        [Test]
        public void カーソルセルの固有理由は従来どおり積む()
        {
            var feedback = new PlacementFeedback();

            BeltConveyorFeedbackReporter.Report(feedback, BeltConveyorPlacementBlockReason.ImpossibleOverpass, false);

            Assert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceBeltOverpassInfeasible.Key }, feedback.Lines.Select(line => line.Key.Key).ToArray());
        }
    }
}
