using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.CleanRoom
{
    /// <summary>
    /// 確率の欄の行（最終の内訳の1組。ADR 0105 §2・§3）
    /// Odds section rows (the single final-odds set; ADR 0105 §2, §3)
    /// </summary>
    public class CleanRoomYieldLogicTest
    {
        private static readonly IReadOnlyList<CleanRoomOddsImprovement> NoImprovements = new List<CleanRoomOddsImprovement>();

        [Test]
        public void 露光は失敗の1行だけを出す()
        {
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldOdds.LayerExposure(0.308, NoImprovements));

            Assert.AreEqual(1, lines.Count);
            AssertLine(lines[0], "clean-room-yield-failed-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { "percent", "30.8" });
        }

        [Test]
        public void ダイシングは出力の個数と表示が0でないLvの割合と失敗を出す()
        {
            // Lv3 は 0.04% で表示が 0.0% になるので行を作らない。Lv4 は 0
            // Lv3 is 0.04%, which shows as 0.0%, so it gets no row; Lv4 is zero
            var lines = CleanRoomYieldLogic.Lines(CleanRoomYieldOdds.Dicing(300, new[] { 0.423, 0.072, 0.0004, 0 }, 0.5046, NoImprovements));

            Assert.AreEqual(4, lines.Count);
            AssertLine(lines[0], "clean-room-yield-output-count", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldOutputCount, new[] { "count", "300" });
            AssertLine(lines[1], "clean-room-yield-level-rate-1", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "1", "percent", "42.3" });
            AssertLine(lines[2], "clean-room-yield-level-rate-2", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "2", "percent", "7.2" });
            AssertLine(lines[3], "clean-room-yield-failed-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { "percent", "50.5" });
        }

        [TestCase(0.33334, 0.33333, 0.33333, "33.4", "33.3", "33.3")]
        [TestCase(0.12356, 0.32157, 0.55487, "12.3", "32.2", "55.5")]
        public void RoundedDistributionTotalsOneHundred(double first, double second, double failed, string firstText, string secondText, string failedText)
        {
            var odds = CleanRoomYieldOdds.Dicing(1, new[] { first, second }, failed, NoImprovements);
            var lines = CleanRoomYieldLogic.Lines(odds);
            var current = CleanRoomYieldImprovementLogic.CurrentValues(odds);

            // 欄とモーダルが同じ合計100%の内訳を使う
            // The section and the modal share a breakdown totaling 100 percent
            AssertLine(lines[1], "clean-room-yield-level-rate-1", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "1", "percent", firstText });
            AssertLine(lines[2], "clean-room-yield-level-rate-2", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { "level", "2", "percent", secondText });
            AssertLine(lines[3], "clean-room-yield-failed-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { "percent", failedText });
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelValue, new[] { "level", "1", "percent", firstText }, current[0]);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelValue, new[] { "level", "2", "percent", secondText }, current[1]);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedValue, new[] { "percent", failedText }, current[2]);
        }

        private static void AssertLine(CleanRoomYieldLine line, string rowId, LocalizationKey key, string[] nameValuePairs)
        {
            Assert.AreEqual(rowId, line.RowId);
            UiTextAssert.AreEqual(key, nameValuePairs, line.Text);
        }
    }
}
