using System;
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    // 行の識別子（VisualElement の name に使う）
    // Row identifier (used as the VisualElement name)
    // 行の識別子（VisualElement の name に使う）
    // Row identifier (used as the VisualElement name)
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
    /// 役割に関係する歩留まりの行だけを作る。割合は小数 1 桁の百分率（Web cleanRoomYieldLogic.ts の写し）
    /// Builds only the yield rows relevant to the role; rates are percentages with one decimal (port of Web cleanRoomYieldLogic.ts)
    /// </summary>
    public static class CleanRoomYieldLogic
    {
        // 出力待ちの確定済み抽選があれば、その値と次の新規抽選の現在値を見出し付きで分けて並べる
        // With a fixed draw awaiting output, list its values and the next draw's current values apart under headings
        public static List<CleanRoomYieldLine> Lines(CleanRoomYieldFetched cleanRoomYield)
        {
            var next = RateLines(cleanRoomYield.Current, "clean-room-yield");
            if (!(cleanRoomYield is PendingDrawCleanRoomYield pending)) return next;

            var lines = new List<CleanRoomYieldLine> { new CleanRoomYieldLine("clean-room-yield-pending-label", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldPendingDrawLabel)) };
            lines.AddRange(RateLines(pending.PendingDraw, "clean-room-yield-pending"));
            lines.Add(new CleanRoomYieldLine("clean-room-yield-next-label", new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldNextDrawLabel)));
            lines.AddRange(next);
            return lines;

            #region Internal

            List<CleanRoomYieldLine> RateLines(CleanRoomYieldRates rates, string rowIdPrefix)
            {
                switch (rates)
                {
                    case LayerExposureYieldRates layer:
                        return new List<CleanRoomYieldLine> { Row($"{rowIdPrefix}-layer-fail-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLayerFailRate, new[] { Percent(layer.LayerFailRate) }) };
                    case DicingYieldRates dicing:
                        return DicingLines(dicing, rowIdPrefix);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(rates), rates.GetType().Name, null);
                }
            }

            // ダイシングは品質 Tier・損失率・0 でないレベル割合・失敗ダイ割合の順。0 のレベルは出ないので行を作らない
            // Dicing lists tier, loss rate, non-zero level shares, then failed dies; zero levels never appear, so they get no row
            List<CleanRoomYieldLine> DicingLines(DicingYieldRates rates, string rowIdPrefix)
            {
                var lines = new List<CleanRoomYieldLine>
                {
                    Row($"{rowIdPrefix}-quality-tier", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldQualityTier, new[] { new LocalizationTextParam("tier", UiNumberFormat.Integer(rates.QualityTier)) }),
                    Row($"{rowIdPrefix}-die-loss-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldDieLossRate, new[] { Percent(rates.DieLossRate) }),
                };
                for (var i = 0; i < rates.LevelRates.Count; i++)
                {
                    if (rates.LevelRates[i] == 0) continue;
                    var level = UiNumberFormat.Integer(i + 1);
                    lines.Add(Row($"{rowIdPrefix}-level-rate-{level}", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldLevelRate, new[] { new LocalizationTextParam("level", level), Percent(rates.LevelRates[i]) }));
                }
                lines.Add(Row($"{rowIdPrefix}-failed-rate", LocalizationKeys.Ui.BlockInventory.CleanRoomYieldFailedRate, new[] { Percent(rates.FailedRate) }));
                return lines;
            }

            CleanRoomYieldLine Row(string rowId, LocalizationKey key, LocalizationTextParam[] textParams)
            {
                return new CleanRoomYieldLine(rowId, new UiText(key, textParams));
            }

            LocalizationTextParam Percent(double rate)
            {
                return new LocalizationTextParam("percent", UiNumberFormat.Fixed(rate * 100, 1));
            }

            #endregion
        }
    }
}
