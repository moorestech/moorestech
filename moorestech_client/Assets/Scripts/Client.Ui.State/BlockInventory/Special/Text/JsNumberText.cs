using System;
using System.Globalization;
using Client.Ui.State.Text;

namespace Client.Ui.State.BlockInventory.Special.Text
{
    /// <summary>
    /// Web が文に入れる数値の JS の書式（toFixed(0)・toPrecision(2)・String(number)）の写し
    /// Port of the JS number formats the Web puts into sentences: toFixed(0), toPrecision(2) and String(number)
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

        // サーバーの float を Web と同じ値の double にする（Newtonsoft の最短表記を JS が double で読む経路）
        // Turns a server float into the double the Web sees (shortest-form text parsed as a JS double)
        public static double FromServerFloat(float value)
        {
            return double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        // String(number): 整数は小数点なし、他は最短の往復表記
        // String(number): integers without a decimal point, others in the shortest round-trip form
        public static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
