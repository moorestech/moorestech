using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    // 確率の欄の1行。行の識別子は VisualElement の name に使う
    // One odds row; the row id is used as the VisualElement name
    public sealed class CleanRoomYieldLine
    {
        public string RowId { get; }
        public UiText Text { get; }

        public CleanRoomYieldLine(string rowId, UiText text)
        {
            RowId = rowId;
            Text = text;
        }
    }

    /// <summary>
    /// 確率の欄の行を作る。割合は小数1桁の百分率で、表示が 0.0% になる Lv は行を作らない（ADR 0105 §2）
    /// Builds the odds rows; shares are one-decimal percentages and a level showing 0.0% gets no row (ADR 0105 §2)
    /// </summary>
    public static class CleanRoomYieldLogic
    {
        // 小数1桁で 0 と表示される値
        // The one-decimal text of zero
        internal const string ZeroPercent = "0.0";

        // 確率はいつも今の条件の1組だけ（出力の空き待ちの組は無い。ADR 0105 §3）
        // Always the single set for current conditions (no output-wait set; ADR 0105 §3)
        public static List<CleanRoomYieldLine> Lines(CleanRoomYieldOdds odds)
        {
            switch (odds)
            {
                case LayerExposureYieldOdds layer:
                    return new List<CleanRoomYieldLine> { FailedLine(layer.FailedRate) };
                case DicingYieldOdds dicing:
                    return DicingLines(dicing);
                default:
                    throw new ArgumentOutOfRangeException(nameof(odds), odds.GetType().Name, null);
            }

            #region Internal

            // 出力の個数・Lv の割合・失敗の順
            // Output count, level shares, then failure
            List<CleanRoomYieldLine> DicingLines(DicingYieldOdds dicing)
            {
                var lines = new List<CleanRoomYieldLine>
                {
                    new("clean-room-yield-output-count", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldOutputCount, new[] { new LocalizationTextParam("count", UiNumberFormat.Integer(dicing.OutputCount)) })),
                };
                var rounded = RoundedDicingShares(dicing);
                foreach (var level in ShownLevels(rounded.Levels))
                {
                    var levelText = UiNumberFormat.Integer(level);
                    var percent = Percent(rounded.Levels[level - 1]);
                    lines.Add(new CleanRoomYieldLine($"clean-room-yield-level-rate-{levelText}", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { new LocalizationTextParam("level", levelText), new LocalizationTextParam("percent", percent) })));
                }
                lines.Add(FailedLine(rounded.Failed));
                return lines;
            }

            CleanRoomYieldLine FailedLine(double failedRate)
            {
                return new CleanRoomYieldLine("clean-room-yield-failed-rate", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { new LocalizationTextParam("percent", Percent(failedRate)) }));
            }

            #endregion
        }

        // 0.1ポイント単位を最大剰余法で配り、表示の合計を100%に保つ
        // Allocate tenths of a percentage point by largest remainder to preserve a displayed total of 100%
        internal static (double[] Levels, double Failed) RoundedDicingShares(DicingYieldOdds odds)
        {
            const int totalUnits = 1000;
            var shares = odds.LevelRates.Append(odds.FailedRate).Select(rate => rate * totalUnits).ToArray();
            var units = shares.Select(share => (int)Math.Floor(share)).ToArray();

            // 同じ端数ならLv順、最後に失敗を優先する
            // Equal remainders keep the input order, from the first level through failure
            var indices = Enumerable.Range(0, shares.Length).OrderByDescending(index => shares[index] - units[index]);
            foreach (var index in indices.Take(totalUnits - units.Sum())) units[index]++;

            var levels = units.Take(odds.LevelRates.Count).Select(count => (double)count / totalUnits).ToArray();
            return (levels, (double)units[odds.LevelRates.Count] / totalUnits);
        }

        // 0..1 の割合を小数1桁の百分率の文にする（モーダルの文も同じ書式）
        // A 0..1 share as a one-decimal percentage text (the modal texts share the format)
        internal static string Percent(double rate)
        {
            return UiNumberFormat.Fixed(rate * 100, 1);
        }

        // 表示（絶対値の小数1桁）が 0.0 になるか。0.0 の判定はここ1か所（行を出すか・符号を付けるかの両方。D-8）
        // Whether the one-decimal magnitude shows 0.0; the only zero check (both row visibility and sign; D-8)
        internal static bool ShowsZero(double value)
        {
            return Percent(Math.Abs(value)) == ZeroPercent;
        }

        // 表示が 0.0 にならない Lv の番号（1 から）。欄の行・「今」の値・改善の差分はどれもこの Lv だけを出す（D-8）
        // Level numbers (from 1) whose value does not show 0.0; section rows, current values and improvement deltas all show only these (D-8)
        internal static List<int> ShownLevels(IReadOnlyList<double> levelValues)
        {
            var levels = new List<int>();
            for (var i = 0; i < levelValues.Count; i++)
            {
                if (ShowsZero(levelValues[i])) continue;
                levels.Add(i + 1);
            }
            return levels;
        }
    }
}
