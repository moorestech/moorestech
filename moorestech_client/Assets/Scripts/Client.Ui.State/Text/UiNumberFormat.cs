using System;
using System.Globalization;
using System.Numerics;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// 表示用の数値書式と、Web の数値の意味に合わせる変換（FromServerFloat）。de などで「,」にならないよう InvariantCulture を明示する（plan.md §3.9）。状態層の純ロジックも使うので State に置く
    /// Display number formatting plus the conversion that matches the Web's number semantics (FromServerFloat); explicit InvariantCulture so locales like de never print ',' (plan.md §3.9); lives in State because state-layer logic uses it too
    /// </summary>
    public static class UiNumberFormat
    {
        // Web の toFixed と同じく、桁区切りなしで小数点は「.」
        // Like the Web's toFixed: no grouping and '.' as the decimal point
        public static string Fixed(double value, int decimals)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsInfinity(value)) return 0 < value ? "Infinity" : "-Infinity";

            // double の正確な 2 進値を半上（絶対値で遠ざかる向き）に丸める。Math.Round は 10 倍した値を丸めるため 2.55 などでずれる
            // Round the exact binary value half-up in magnitude; Math.Round scales first and drifts on values like 2.55
            var bits = BitConverter.DoubleToInt64Bits(value);
            var exponentBits = (int)((bits >> 52) & 0x7FF);
            var mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exponentBits != 0) mantissa |= 1L << 52;
            var exponent = (exponentBits == 0 ? 1 : exponentBits) - 1075;

            var scaled = new BigInteger(mantissa) * BigInteger.Pow(10, decimals);
            var digitsValue = 0 <= exponent
                ? scaled << exponent
                : ((scaled << 1) + (BigInteger.One << -exponent)) >> (1 - exponent);

            // 桁を整数部と小数部へ分ける。負の微小値は toFixed と同じく「-0.0」になる
            // Split into integer and fraction digits; tiny negatives print "-0.0" like toFixed
            var digits = digitsValue.ToString(CultureInfo.InvariantCulture).PadLeft(decimals + 1, '0');
            var sign = value < 0 ? "-" : "";
            if (decimals == 0) return sign + digits;
            return sign + digits.Substring(0, digits.Length - decimals) + "." + digits.Substring(digits.Length - decimals);
        }

        // サーバーの float を Web と同じ値の double にする（Newtonsoft の最短表記を JS が double で読む経路）
        // Turns a server float into the double the Web sees (shortest-form text parsed as a JS double)
        public static double FromServerFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value == 0) return value;
            for (var precision = 1; precision <= 9; precision++)
            {
                var text = value.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
                if (float.Parse(text, CultureInfo.InvariantCulture) == value) return double.Parse(text, CultureInfo.InvariantCulture);
            }
            // float は 9 桁で必ず往復するのでここへは来ない
            // A float always round-trips within 9 digits, so this is unreachable
            throw new InvalidOperationException($"float {value:R} did not round-trip within 9 digits");
        }

        // JS の String(number): 整数は小数点なし、他は最短の往復表記、1e21 以上と 1e-7 未満は「1e+21」「1e-7」の指数表記
        // JS String(number): integers without a decimal point, others shortest round-trip, exponent form "1e+21" / "1e-7" from 1e21 up and below 1e-6
        public static string JsString(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsInfinity(value)) return 0 < value ? "Infinity" : "-Infinity";
            if (value == 0) return "0";

            // 最短で往復できる有効桁を探し、桁列と「値 = 桁列 × 10^(n-k)」の n を得る
            // Find the shortest round-trip digits, then n where value = digits x 10^(n-k)
            var magnitude = Math.Abs(value);
            var text = "";
            for (var precision = 1; precision <= 17; precision++)
            {
                text = magnitude.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
                if (double.Parse(text, CultureInfo.InvariantCulture) == magnitude) break;
            }
            var exponentIndex = text.IndexOf('E');
            var digits = text.Substring(0, exponentIndex).Replace(".", "");
            var n = int.Parse(text.Substring(exponentIndex + 1), CultureInfo.InvariantCulture) + 1;
            var k = digits.Length;

            // JS Number::toString の分岐（21 桁まで素の整数、-6 まで素の小数、他は指数）
            // The JS Number::toString branches: plain integer up to 21 digits, plain decimal down to -6, exponent otherwise
            var sign = value < 0 ? "-" : "";
            if (k <= n && n <= 21) return sign + digits + new string('0', n - k);
            if (0 < n && n <= 21) return sign + digits.Substring(0, n) + "." + digits.Substring(n);
            if (-6 < n && n <= 0) return sign + "0." + new string('0', -n) + digits;
            var exponent = n - 1;
            var exponentText = (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
            return sign + (k == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1)) + "e" + exponentText;
        }

        public static string Integer(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Web の Math.round と同じく .5 は正の無限大側へ丸める（C# 既定の偶数丸めとは違う）
        // Like the Web's Math.round, .5 rounds toward positive infinity (unlike C#'s default banker's rounding)
        public static long RoundHalfUp(double value)
        {
            // value + 0.5 は 0.49999999999999994 で 1.0 に丸まるため、小数部で判定する
            // value + 0.5 rounds 0.49999999999999994 up to 1.0, so decide by the fractional part
            var floor = Math.Floor(value);
            return (long)(0.5 <= value - floor ? floor + 1 : floor);
        }
    }
}
