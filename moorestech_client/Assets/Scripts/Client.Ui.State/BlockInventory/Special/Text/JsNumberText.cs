using System;
using System.Globalization;
using Client.Ui.State.Text;

namespace Client.Ui.State.BlockInventory.Special.Text
{
    /// <summary>
    /// 特殊区画の文の JS 数値書式の写し
    /// Port of JS number formats for special sentences
    /// </summary>
    public static class JsNumberText
    {
        public static string Fixed0(double value)
        {
            return UiNumberFormat.Fixed(value, 0);
        }

        // 正の微小値は 0 に潰さず有効 2 桁で残す（Web GearToElectricSection の formatPower）
        // Keep a positive tiny value at two significant digits instead of 0 (Web GearToElectricSection formatPower)
        public static string PositivePower(double power)
        {
            var rounded = Fixed0(power);
            return 0 < power && rounded == "0" ? Precision2(power) : rounded;
        }

        // toPrecision(2): 指数が -6 より小さいか 2 以上なら指数表記（符号付き）、他は小数（JS の規則）
        // toPrecision(2): signed exponent notation when the exponent is below -6 or at least 2, plain decimals otherwise (JS rule)
        internal static string Precision2(double value)
        {
            if (value == 0) return "0.0";
            var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            var mantissa = Math.Round(value / Math.Pow(10, exponent), 1, MidpointRounding.AwayFromZero);
            if (10 <= Math.Abs(mantissa))
            {
                mantissa /= 10;
                exponent += 1;
            }
            if (exponent < -6 || 2 <= exponent) return $"{mantissa.ToString("0.0", CultureInfo.InvariantCulture)}e{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent)}";

            // 小数は有効 2 桁ぶんの桁数で丸める
            // Plain decimals round to the digits that keep two significant figures
            var decimals = 1 - exponent;
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
        }
    }
}
