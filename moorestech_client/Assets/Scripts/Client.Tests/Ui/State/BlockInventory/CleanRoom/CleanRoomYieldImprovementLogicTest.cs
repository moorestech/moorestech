using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.CleanRoom
{
    /// <summary>
    /// 「失敗率の改善方法」の今の値と改善ごとの差分の文（ADR 0105 §4）
    /// Current values and per-improvement delta texts of the improvement modal (ADR 0105 §4)
    /// </summary>
    public class CleanRoomYieldImprovementLogicTest
    {
        private const string Minus = "−";

        [Test]
        public void 今の値は表示が0でないLvと失敗を短い形で並べる()
        {
            var values = CleanRoomYieldImprovementLogic.CurrentValues(CleanRoomYieldOdds.Dicing(300, new[] { 0.423, 0.072, 0 }, 0.505, Improvements()));

            Assert.AreEqual(3, values.Count);
            AssertLevel(values[0], "1", "42.3");
            AssertLevel(values[1], "2", "7.2");
            AssertFailed(values[2], "50.5");
        }

        [Test]
        public void 露光の今の値は失敗だけ()
        {
            var values = CleanRoomYieldImprovementLogic.CurrentValues(CleanRoomYieldOdds.LayerExposure(0.308, new List<CleanRoomOddsImprovement>()));

            Assert.AreEqual(1, values.Count);
            AssertFailed(values[0], "30.8");
        }

        [Test]
        public void 改善の行は項目名と符号つきの差分で_表示が0のLvは出さず失敗はいつも出す()
        {
            var rows = CleanRoomYieldImprovementLogic.Rows(CleanRoomYieldOdds.Dicing(300, new[] { 0.423, 0.072, 0 }, 0.505, Improvements()));

            Assert.AreEqual(3, rows.Count);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImproveRoomClass, new[] { "className", "B" }, rows[0].Label);
            Assert.AreEqual(3, rows[0].Values.Count);
            AssertLevel(rows[0].Values[0], "1", "+9.2");
            AssertLevel(rows[0].Values[1], "2", "+3.8");
            AssertFailed(rows[0].Values[2], Minus + "13.1");

            // 1つ上の品質モジュールは Lv1 が減り Lv2・Lv3 が増える
            // The next-tier module lowers Lv1 and raises Lv2 and Lv3
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImproveAddModule, new[] { "tier", "2" }, rows[1].Label);
            Assert.AreEqual(4, rows[1].Values.Count);
            AssertLevel(rows[1].Values[0], "1", Minus + "36.9");
            AssertLevel(rows[1].Values[1], "2", "+37.0");
            AssertLevel(rows[1].Values[2], "3", "+8.2");
            AssertFailed(rows[1].Values[3], Minus + "8.2");

            // 差分がどれも表示 0.0 の行は失敗の「0.0」だけ（符号なし）
            // A row whose deltas all show 0.0 keeps only the failure "0.0" (no sign)
            Assert.AreEqual(1, rows[2].Values.Count);
            AssertFailed(rows[2].Values[0], "0.0");
        }

        [Test]
        public void 露光の改善の行は失敗の差分だけ()
        {
            var improvements = new List<CleanRoomOddsImprovement> { CleanRoomOddsImprovement.RoomClassUp("A", new double[0], -0.1) };
            var rows = CleanRoomYieldImprovementLogic.Rows(CleanRoomYieldOdds.LayerExposure(0.2, improvements));

            Assert.AreEqual(1, rows[0].Values.Count);
            AssertFailed(rows[0].Values[0], Minus + "10.0");
        }

        private static List<CleanRoomOddsImprovement> Improvements()
        {
            return new List<CleanRoomOddsImprovement>
            {
                CleanRoomOddsImprovement.RoomClassUp("B", new[] { 0.092, 0.038, 0.0 }, -0.131),
                CleanRoomOddsImprovement.AddQualityModule(2, new[] { -0.369, 0.370, 0.082 }, -0.082),
                CleanRoomOddsImprovement.AddQualityModule(1, new[] { 0.0, 0.0002, 0.0 }, 0.0001),
            };
        }

        private static void AssertLevel(UiText text, string level, string percent)
        {
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelValue, new[] { "level", level, "percent", percent }, text);
        }

        private static void AssertFailed(UiText text, string percent)
        {
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedValue, new[] { "percent", percent }, text);
        }
    }
}
