using Client.Ui.State.BlockInventory.Special.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// JS の数値書式（toFixed(0)・toPrecision(2)・String(number)）を Web と同じ文字にする
    /// Formats numbers exactly like JS toFixed(0), toPrecision(2) and String(number)
    /// </summary>
    public class JsNumberTextTest
    {
        [TestCase(0d, "0")]
        [TestCase(0.1d, "0.10")]
        [TestCase(0.49d, "0.49")]
        [TestCase(0.5d, "1")]
        [TestCase(29.49999d, "29")]
        [TestCase(29.5d, "30")]
        [TestCase(1e-8d, "1.0e-8")]
        [TestCase(0.2d, "0.20")]
        [TestCase(100d, "100")]
        public void 正の微小値は有効2桁で残し他は整数に丸める(double power, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.PositivePower(power));
        }

        [TestCase(0.000123d, "0.00012")]
        [TestCase(0.0000012d, "0.0000012")]
        [TestCase(1.5e-7d, "1.5e-7")]
        [TestCase(0.095d, "0.095")]
        [TestCase(150d, "1.5e+2")]
        public void 有効2桁はJSのtoPrecisionと同じ表記(double value, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.Precision2(value));
        }

        [TestCase(10d, "10")]
        [TestCase(2.5d, "2.5")]
        [TestCase(0.1d, "0.1")]
        public void 素の数値はJSのStringと同じ表記(double value, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.Number(value));
        }
    }
}
