using System.Globalization;
using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// JS の String(number) と、サーバー float の double 補正が Web と同じ文字になることを確かめる（L0。de-DE でも「.」）
    /// Verifies JS String(number) and the server-float correction give the Web's text (L0; '.' even under de-DE)
    /// </summary>
    public class UiNumberFormatJsStringTest
    {
        private CultureInfo _savedCulture;

        [SetUp]
        public void SetUp()
        {
            _savedCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _savedCulture;
        }

        [TestCase(0d, "0")]
        [TestCase(-0d, "0")]
        [TestCase(10d, "10")]
        [TestCase(-10d, "-10")]
        [TestCase(2.5d, "2.5")]
        [TestCase(-2.5d, "-2.5")]
        [TestCase(0.1d, "0.1")]
        [TestCase(0.3d, "0.3")]
        [TestCase(0.1d + 0.2d, "0.30000000000000004")]
        [TestCase(60d / 7d, "8.571428571428571")]
        [TestCase(123456789012345680000d, "123456789012345680000")]
        [TestCase(1e21d, "1e+21")]
        [TestCase(1.5e21d, "1.5e+21")]
        [TestCase(-1e21d, "-1e+21")]
        [TestCase(0.000001d, "0.000001")]
        [TestCase(0.0000012d, "0.0000012")]
        [TestCase(1e-7d, "1e-7")]
        [TestCase(1.5e-7d, "1.5e-7")]
        [TestCase(-1e-7d, "-1e-7")]
        public void JsStringはJSのStringと同じ文字(double value, string expected)
        {
            Assert.AreEqual(expected, UiNumberFormat.JsString(value));
        }

        [Test]
        public void JsStringは非有限をJSの綴りで返す()
        {
            Assert.AreEqual("NaN", UiNumberFormat.JsString(double.NaN));
            Assert.AreEqual("Infinity", UiNumberFormat.JsString(double.PositiveInfinity));
            Assert.AreEqual("-Infinity", UiNumberFormat.JsString(double.NegativeInfinity));
        }

        [Test]
        public void サーバーfloatは最短表記のdoubleになり電力が長い桁で出ない()
        {
            // float 33.333332f をそのまま double にすると 33.33333206176758、Web は JSON の最短表記を読むので 33.333332
            // A raw double of 33.333332f is 33.33333206176758; the Web reads the shortest JSON form, 33.333332
            Assert.AreEqual("33.333332", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(33.333332f)));
            Assert.AreEqual("0.1", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(0.1f)));
            Assert.AreEqual("-12.5", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(-12.5f)));
            Assert.AreEqual("100", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(100f)));
            Assert.AreEqual(0d, UiNumberFormat.FromServerFloat(0f));
        }

        [Test]
        public void サーバーfloatは9桁要る値でもWebと同じ文字になる()
        {
            // 最短表記が 9 桁の float。8 桁までで打ち切ると 10.138720512390137 のような長い桁が出る
            // Floats whose shortest form needs 9 digits; stopping at 8 would print long tails like 10.138720512390137
            Assert.AreEqual("12.2914095", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(12.291409492492676f)));
            Assert.AreEqual("0.112309806", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(0.11230980604887009f)));
            Assert.AreEqual("10.1387205", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(10.1387205f)));
        }

        [Test]
        public void サーバーfloatの小数1桁はWebと同じ丸めになる()
        {
            // 12.35f は 12.350000381 で「12.4」になるが、Web は 12.35（実体 12.3499…）を toFixed(1) して「12.3」
            // 12.35f is 12.350000381 and would print "12.4"; the Web toFixes 12.35 (really 12.3499...) to "12.3"
            Assert.AreEqual("12.4", UiNumberFormat.Fixed(12.35f, 1));
            Assert.AreEqual("12.3", UiNumberFormat.Fixed(UiNumberFormat.FromServerFloat(12.35f), 1));
        }
    }
}
