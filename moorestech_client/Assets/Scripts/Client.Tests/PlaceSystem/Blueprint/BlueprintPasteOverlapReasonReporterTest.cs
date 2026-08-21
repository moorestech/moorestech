using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Blueprint
{
    /// <summary>
    ///     BP貼り付けの重複理由が「全セル重複のときだけ」出て、部分重複では出ないことを検証
    ///     Verify the BP paste overlap reason appears only when every cell overlaps, and never on partial overlap
    /// </summary>
    public class BlueprintPasteOverlapReasonReporterTest
    {
        [Test]
        public void 全セルが重複していれば設置位置が埋まっている行を積む()
        {
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new[] { false, false, false }, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].TextKey);
        }

        [Test]
        public void 一部のセルだけ重複していても何も積まない()
        {
            // 部分重複は設置可能セルだけを送る既存挙動のままで、案内行は出さない
            // Partial overlap keeps the existing placeable-cells-only send and produces no line
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
            // 未解決BP等でセルが0件のときに空のBPを埋まっている扱いにしない
            // Zero cells (e.g. an unresolved blueprint) must not be reported as fully occupied
            var feedback = new PlacementFeedback();

            BlueprintPasteOverlapReasonReporter.Report(new List<bool>(), feedback);

            Assert.IsEmpty(feedback.Lines);
        }
    }
}
