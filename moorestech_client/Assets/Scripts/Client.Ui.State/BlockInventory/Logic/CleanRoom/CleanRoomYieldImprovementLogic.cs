using System;
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    // 改善の1行: 何をするか（項目名）と、今との差分の並び
    // One improvement row: what to do (label) and the deltas from now
    public sealed class CleanRoomYieldImprovementRow
    {
        public UiText Label { get; }
        public IReadOnlyList<UiText> Values { get; }

        public CleanRoomYieldImprovementRow(UiText label, IReadOnlyList<UiText> values)
        {
            Label = label;
            Values = values;
        }
    }

    /// <summary>
    /// 「失敗率の改善方法」の今の値と改善ごとの差分を作る。差分は符号つき小数1桁で、表示が 0.0 の Lv は出さず失敗はいつも出す
    /// Builds the current values and per-improvement deltas; deltas are signed one-decimal, a level showing 0.0 is dropped and failure always shows
    /// </summary>
    public static class CleanRoomYieldImprovementLogic
    {
        // 減る向きは全角幅のマイナス記号（U+2212。ADR 0105 §4 の例）
        // The decrease sign is the minus sign U+2212 (ADR 0105 §4 example)
        private const string MinusSign = "−";

        // 「今: Lv1 42.3% / Lv2 7.2% / 失敗 50.5%」の値の並び（欄と同じく表示が 0.0% の Lv は出さない）
        // Values of "Now: Lv1 42.3% / Lv2 7.2% / Failed 50.5%" (levels showing 0.0% are dropped like the section)
        public static IReadOnlyList<UiText> CurrentValues(CleanRoomYieldOdds odds)
        {
            var values = new List<UiText>();
            switch (odds)
            {
                case LayerExposureYieldOdds layer:
                    values.Add(FailedValue(CleanRoomYieldLogic.Percent(layer.FailedRate)));
                    return values;
                case DicingYieldOdds dicing:
                    foreach (var level in CleanRoomYieldLogic.ShownLevels(dicing.LevelRates)) values.Add(LevelValue(level, CleanRoomYieldLogic.Percent(dicing.LevelRates[level - 1])));
                    values.Add(FailedValue(CleanRoomYieldLogic.Percent(dicing.FailedRate)));
                    return values;
                default:
                    throw new ArgumentOutOfRangeException(nameof(odds), odds.GetType().Name, null);
            }
        }

        // サーバーの並びのまま、改善ごとに項目名と差分を作る
        // Builds a label and deltas per improvement, in server order
        public static IReadOnlyList<CleanRoomYieldImprovementRow> Rows(CleanRoomYieldOdds odds)
        {
            var rows = new List<CleanRoomYieldImprovementRow>();
            foreach (var improvement in odds.Improvements) rows.Add(new CleanRoomYieldImprovementRow(Label(improvement), Deltas(improvement)));
            return rows;

            #region Internal

            UiText Label(CleanRoomOddsImprovement improvement)
            {
                switch (improvement)
                {
                    case RoomClassUpOddsImprovement classUp:
                        return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImproveRoomClass, new[] { new LocalizationTextParam("className", classUp.TargetClassName) });
                    case AddQualityModuleOddsImprovement module:
                        return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImproveAddModule, new[] { new LocalizationTextParam("tier", UiNumberFormat.Integer(module.ModuleTier)) });
                    default:
                        throw new ArgumentOutOfRangeException(nameof(improvement), improvement.GetType().Name, null);
                }
            }

            // 表示が 0.0 になる Lv の差分は出さず（変わらない Lv で行が長くなるため）、失敗の差分はいつも出す（失敗率の改善が主題のため）
            // Drop level deltas showing 0.0 (unchanged levels only lengthen the line); always show the failure delta (failure is the subject)
            IReadOnlyList<UiText> Deltas(CleanRoomOddsImprovement improvement)
            {
                var values = new List<UiText>();
                foreach (var level in CleanRoomYieldLogic.ShownLevels(improvement.LevelRateDeltas)) values.Add(LevelValue(level, Signed(improvement.LevelRateDeltas[level - 1])));
                values.Add(FailedValue(Signed(improvement.FailedRateDelta)));
                return values;
            }

            // 絶対値を小数1桁にし、0.0 でなければ +/− を付ける
            // One-decimal magnitude with + or − unless it shows 0.0
            string Signed(double delta)
            {
                if (CleanRoomYieldLogic.ShowsZero(delta)) return CleanRoomYieldLogic.ZeroPercent;
                return (0 < delta ? "+" : MinusSign) + CleanRoomYieldLogic.Percent(Math.Abs(delta));
            }

            #endregion
        }

        private static UiText LevelValue(int level, string percent)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelValue, new[] { new LocalizationTextParam("level", UiNumberFormat.Integer(level)), new LocalizationTextParam("percent", percent) });
        }

        private static UiText FailedValue(string percent)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedValue, new[] { new LocalizationTextParam("percent", percent) });
        }
    }
}
