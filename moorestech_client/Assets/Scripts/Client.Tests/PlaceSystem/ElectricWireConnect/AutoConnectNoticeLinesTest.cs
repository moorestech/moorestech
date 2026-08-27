using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    ///     自動接続プレビューの案内行の判断をテーブルで検証
    ///     Verify the auto-connect preview's notice-line judgement with a table
    /// </summary>
    public class AutoConnectNoticeLinesTest
    {
        // cursorWirePlaceable / cursorRawTargetCount / hasOutOfRangeNeighbor / totalCost / 期待キー(nullは行なし) / 期待戻り値
        // cursorWirePlaceable / cursorRawTargetCount / hasOutOfRangeNeighbor / totalCost / expected key (null means no line) / expected return
        private static readonly object[] ReportCases =
        {
            new object[] { false, 0, false, 0, LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, true },
            new object[] { false, 2, true, 5, LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, true },
            new object[] { true, 0, true, 0, LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice.Key, false },
            new object[] { true, 0, true, 5, LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice.Key, false },
            new object[] { true, 0, false, 0, null, false },
            new object[] { true, 0, false, 5, LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, false },
            new object[] { true, 2, true, 5, LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, false },
            new object[] { true, 2, false, 0, null, false },
        };

        [TestCaseSource(nameof(ReportCases))]
        public void カーソルセル状態ごとに積む案内行と戻り値が決まる(bool cursorWirePlaceable, int cursorRawTargetCount, bool hasOutOfRangeNeighbor, int totalCost, string expectedKey, bool expectedIsWireShortage)
        {
            var feedback = new PlacementFeedback();

            var isWireShortage = AutoConnectNoticeLines.Report(cursorWirePlaceable, cursorRawTargetCount, hasOutOfRangeNeighbor, totalCost, feedback);

            Assert.AreEqual(expectedIsWireShortage, isWireShortage);
            CollectionAssert.AreEqual(expectedKey == null ? Array.Empty<string>() : new[] { expectedKey }, feedback.Lines.Select(line => line.Key.Key).ToArray());
        }

        [Test]
        public void 電線コスト行は合計消費数を文言に載せる()
        {
            var feedback = new PlacementFeedback();

            AutoConnectNoticeLines.Report(true, 2, false, 7, feedback);

            CollectionAssert.AreEqual(new[] { "7" }, feedback.Lines[0].TextParams);
        }

        // 近傍走査は「配線される相手が1件も無い」ときだけ意味を持つ
        // The neighbor scan only matters when no target would be wired at all
        [TestCase(true, 0, true)]
        [TestCase(true, 1, false)]
        [TestCase(false, 0, false)]
        [TestCase(false, 1, false)]
        public void 範囲外案内の近傍走査は配線先が無く電線も足りているときだけ要る(bool cursorWirePlaceable, int cursorRawTargetCount, bool expected)
        {
            Assert.AreEqual(expected, AutoConnectNoticeLines.NeedsOutOfRangeProbe(cursorWirePlaceable, cursorRawTargetCount));
        }
    }
}
