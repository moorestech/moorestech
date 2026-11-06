using System;
using System.Globalization;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// 表示用の数値書式。de などで「,」にならないよう InvariantCulture を明示する（plan.md §3.9）。状態層の純ロジックも使うので State に置く
    /// Display number formatting with explicit InvariantCulture so locales like de never print ',' (plan.md §3.9); lives in State because state-layer logic uses it too
    /// </summary>
    public static class UiNumberFormat
    {
        // Web の toFixed と同じく、桁区切りなしで小数点は「.」
        // Like the Web's toFixed: no grouping and '.' as the decimal point
        public static string Fixed(double value, int decimals)
        {
            var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            return rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string Integer(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Web の Math.round と同じく .5 は正の無限大側へ丸める（C# 既定の偶数丸めとは違う）
        // Like the Web's Math.round, .5 rounds toward positive infinity (unlike C#'s default banker's rounding)
        public static long RoundHalfUp(double value)
        {
            return (long)Math.Floor(value + 0.5);
        }
    }
}
