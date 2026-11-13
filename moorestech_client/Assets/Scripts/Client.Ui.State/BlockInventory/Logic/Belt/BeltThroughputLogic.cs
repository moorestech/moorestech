using System;
using System.Collections.Generic;
using Client.Localization;
using Core.Update;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Belt
{
    public enum BeltWindowStep
    {
        Previous = -1,
        Next = 1,
    }

    // 行の文。毎分の搬出数（小数 1 桁）は計測済みの行にだけある
    // The row text; exits per minute (one decimal) exist only on a measured row
    public abstract class BeltThroughputRowDisplay
    {
        public UiText Text { get; }

        private protected BeltThroughputRowDisplay(UiText text)
        {
            Text = text;
        }

        public static BeltThroughputRowDisplay Measuring(UiText text)
        {
            return new MeasuringBeltRowDisplay(text);
        }

        public static BeltThroughputRowDisplay Measured(UiText text, string itemsPerMinute)
        {
            return new MeasuredBeltRowDisplay(text, itemsPerMinute);
        }
    }

    public sealed class MeasuringBeltRowDisplay : BeltThroughputRowDisplay
    {
        internal MeasuringBeltRowDisplay(UiText text) : base(text)
        {
        }
    }

    public sealed class MeasuredBeltRowDisplay : BeltThroughputRowDisplay
    {
        public string ItemsPerMinute { get; }

        internal MeasuredBeltRowDisplay(UiText text, string itemsPerMinute) : base(text)
        {
            ItemsPerMinute = itemsPerMinute;
        }
    }

    /// <summary>
    /// ベルトの搬出統計の表示判断（Web beltThroughputLogic.ts の写し）
    /// Display decisions for belt exit statistics (port of Web beltThroughputLogic.ts)
    /// </summary>
    public static class BeltThroughputLogic
    {
        // 既定の窓は 180tick（9 秒）。バニラ 3 速度の搬出周期（20・9・3tick）の最小公倍数（ユーザー裁定 2026-10-20）
        // The default window is 180 ticks (9 s), the LCM of the three vanilla output periods (user ruling 2026-10-20)
        public const int DefaultBeltWindowTicks = 180;

        // 棒グラフは 1 秒平均を 9 本（ユーザー裁定 2026-10-20）
        // The bar chart shows nine one-second averages (user ruling 2026-10-20)
        public const int RecentSecondBarCount = 9;

        // 選んだ長さの窓。一覧に無ければ先頭の窓へ落とし、一覧が空なら選べない
        // The window of the chosen length; falls back to the first, and none can be chosen from an empty list
        public static bool TrySelectBeltWindow(IReadOnlyList<BeltThroughputWindow> windows, int ticks, out BeltThroughputWindow selected)
        {
            foreach (var window in windows)
            {
                if (window.Ticks != ticks) continue;
                selected = window;
                return true;
            }
            selected = 0 < windows.Count ? windows[0] : null;
            return 0 < windows.Count;
        }

        // 一覧の中で 1 つ隣の窓へ移る。端では止まり、一覧に無い長さからは先頭扱いで動く
        // Move to the neighbouring window; stops at the ends, and an unlisted length counts as the first
        public static int CycleBeltWindow(IReadOnlyList<BeltThroughputWindow> windows, int currentTicks, BeltWindowStep step)
        {
            if (windows.Count == 0) return currentTicks;
            var currentIndex = 0;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].Ticks != currentTicks) continue;
                currentIndex = i;
                break;
            }
            var nextIndex = Math.Min(windows.Count - 1, Math.Max(0, currentIndex + (int)step));
            return windows[nextIndex].Ticks;
        }

        public static BeltThroughputRowDisplay RowDisplay(BeltThroughputWindow window)
        {
            var seconds = new LocalizationTextParam("seconds", FormatCount(window.Ticks / (double)GameUpdater.TicksPerSecond));
            if (!(window is MeasuredBeltWindow measured)) return new MeasuringBeltRowDisplay(new UiText(LocalizationKeys.Ui.BlockInventory.BeltThroughputMeasuring, new[] { seconds }));

            // 停止中（最大 0）は割合を出せないので 0% にする
            // A stopped belt (max 0) has no ratio, so it shows 0%
            var percent = 0 < measured.MaxCount ? UiNumberFormat.RoundHalfUp(measured.ExitCount / measured.MaxCount * 100) : 0;
            var text = new UiText(LocalizationKeys.Ui.BlockInventory.BeltThroughputWindow, new[]
            {
                seconds,
                new LocalizationTextParam("count", UiNumberFormat.Integer(measured.ExitCount)),
                new LocalizationTextParam("max", FormatCount(measured.MaxCount)),
                new LocalizationTextParam("percent", UiNumberFormat.Integer(percent)),
            });
            var itemsPerMinute = UiNumberFormat.Fixed(measured.ExitCount * (GameUpdater.TicksPerSecond * BlockDetailMath.SecondsPerMinute) / measured.Ticks, 1);
            return new MeasuredBeltRowDisplay(text, itemsPerMinute);

            #region Internal

            // 整数はそのまま、端数は小数 1 桁（90tick=4.5 秒。速度によっては最大数も端数になる）
            // Whole numbers as they are, fractions to one decimal (90 ticks = 4.5 s; some speeds give a fractional maximum)
            string FormatCount(double value)
            {
                return value == Math.Floor(value) ? UiNumberFormat.Integer((long)value) : UiNumberFormat.Fixed(value, 1);
            }

            #endregion
        }

        // 古い順の 1 秒ごとの搬出数を、最大に対する高さ（0..1）の棒 barCount 本に写す。まだ無い秒は先頭側を null で埋める
        // Map oldest-first per-second counts onto barCount bar heights (0..1); missing seconds are null at the front
        public static double?[] RecentSecondBars(IReadOnlyList<int> counts, double maxCountPerSecond, int barCount)
        {
            var bars = new double?[barCount];
            var recentCount = Math.Min(barCount, counts.Count);
            var offset = barCount - recentCount;
            for (var i = 0; i < recentCount; i++)
            {
                // 停止中（最大 0）は 0。整数の粗さで最大を超えた秒は満杯に丸める
                // A stopped belt (max 0) gives 0; a second above the maximum through integer coarseness rounds to full
                var count = counts[counts.Count - recentCount + i];
                bars[offset + i] = 0 < maxCountPerSecond ? Math.Min(1, count / maxCountPerSecond) : 0;
            }
            return bars;
        }
    }
}
