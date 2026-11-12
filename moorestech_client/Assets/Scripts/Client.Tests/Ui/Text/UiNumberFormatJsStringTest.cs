using System.Globalization;
using Client.WebUiHost.Common;
using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// JS の文字列化が Web と同じ（L0）
    /// JS String(number) and float correction match Web (L0)
    /// </summary>
    public class UiNumberFormatJsStringTest
    {
        private class FloatHolder { public float Value; }
        private class DoubleHolder { public double Value; }

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
        public void サーバーfloatはWebUiJsonが書くRの表記のdoubleになり電力が長い桁で出ない()
        {
            // float 33.333332f をそのまま double にすると 33.33333206176758、Web は WebUiJson が書く R 表記（Mono は G7 で往復しなければ G9）を読むので 33.3333321
            // A raw double of 33.333332f is 33.33333206176758; the Web reads the R form WebUiJson writes (Mono: G7 if it round-trips, else G9), 33.3333321
            Assert.AreEqual("33.3333321", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(33.333332f)));
            Assert.AreEqual("0.1", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(0.1f)));
            Assert.AreEqual("-12.5", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(-12.5f)));
            Assert.AreEqual("100", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(100f)));
            Assert.AreEqual(0d, UiNumberFormat.FromServerFloat(0f));
        }

        [Test]
        public void サーバーfloatは9桁要る値でもWebと同じ文字になる()
        {
            // 9 桁 float を 8 桁で切らない
            // 9-digit floats must not be cut at 8 (long tails)
            Assert.AreEqual("12.2914095", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(12.291409492492676f)));
            Assert.AreEqual("0.112309806", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(0.11230980604887009f)));
            Assert.AreEqual("10.1387205", UiNumberFormat.JsString(UiNumberFormat.FromServerFloat(10.1387205f)));
        }

        [Test]
        public void サーバーfloatの小数1桁はWebと同じ丸めになる()
        {
            // 12.35f は Web で 12.3
            // 12.35f is 12.3 on Web (toFixed(1))
            Assert.AreEqual("12.4", UiNumberFormat.Fixed(12.35f, 1));
            Assert.AreEqual("12.3", UiNumberFormat.Fixed(UiNumberFormat.FromServerFloat(12.35f), 1));
        }

        [TestCase(100000.05f)]
        [TestCase(33.333332f)]
        [TestCase(12.35f)]
        public void サーバーfloatはWebUiJsonのJSONをdoubleで読んだ値と一致する(float value)
        {
            // Web は WebUiJson が書いた float の表記を double で読むので、その値と FromServerFloat が同じになる
            // The Web reads the float text WebUiJson wrote as a double, so FromServerFloat must equal that value
            var json = WebUiJson.Serialize(new FloatHolder { Value = value });
            var webValue = WebUiJson.Deserialize<DoubleHolder>(json).Value;
            Assert.AreEqual(webValue, UiNumberFormat.FromServerFloat(value));
        }

        [Test]
        public void JsStringとFromServerFloatは型の最大値でも例外を投げない()
        {
            // 最大値付近は探索の候補が型の範囲を越えるので、例外でなく往復しない扱いで進める
            // Near the maximum the probing candidates overflow the type, so they must count as non-round-tripping instead of throwing
            Assert.AreEqual("1.7976931348623157e+308", UiNumberFormat.JsString(double.MaxValue));
            Assert.AreEqual("-1.7976931348623157e+308", UiNumberFormat.JsString(-double.MaxValue));
            Assert.DoesNotThrow(() => UiNumberFormat.FromServerFloat(float.MaxValue));
        }
    }
}
