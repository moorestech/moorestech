using System;
using System.Globalization;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 表示用の数値書式。de などで「,」にならないよう InvariantCulture を明示する（plan.md §3.9）
    /// Display number formatting with explicit InvariantCulture so locales like de never print ',' (plan.md §3.9)
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
    }
}
