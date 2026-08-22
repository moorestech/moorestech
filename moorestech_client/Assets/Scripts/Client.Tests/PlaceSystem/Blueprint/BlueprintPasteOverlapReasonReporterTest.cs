using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Blueprint
{
    /// <summary>
    ///     全セル重複時のみ理由が出ることを検証
    ///     Verify the reason appears only on full overlap
    /// </summary>
    public class BlueprintPasteOverlapReasonReporterTest
    {
        [Test]
        public void 全セルが重複していれば設置位置が埋まっている行を積む()
        {
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new[] { false, false, false }, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 一部のセルだけ重複していても何も積まない()
        {
            // 部分重複は既存どおり送信し案内行は出さない
            // Partial overlap sends as before and produces no line
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new[] { true, false, false }, feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 全セルが設置可能なら何も積まない()
        {
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new[] { true, true }, feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void セルが空なら何も積まない()
        {
            // セル0件を埋まっている扱いにしない
            // Zero cells must not be reported as fully occupied
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new List<bool>(), feedback);

            Assert.IsEmpty(feedback.Lines);
        }
    }
}
