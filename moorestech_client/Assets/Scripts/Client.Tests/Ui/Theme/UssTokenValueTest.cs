using Client.Ui.Foundation.Theme;
using NUnit.Framework;

namespace Client.Tests.Ui.Theme
{
    public class UssTokenValueTest
    {
        [TestCase("12px", 12f)]
        [TestCase("11.742px", 11.742f)]
        [TestCase("8", 8f)]
        public void ParsesPixels(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParsePixels(text, out var pixels));
            Assert.AreEqual(expected, pixels, 0.0001f);
        }

        [TestCase("160ms", 160f)]
        [TestCase("0.2s", 200f)]
        [TestCase("1200", 1200f)]
        public void ParsesMilliseconds(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParseMilliseconds(text, out var milliseconds));
            Assert.AreEqual(expected, milliseconds, 0.0001f);
        }

        [TestCase("12%")]
        [TestCase("auto")]
        [TestCase("")]
        public void RejectsNonLengths(string text)
        {
            Assert.IsFalse(UssTokenValue.TryParsePixels(text, out _));
        }

        [TestCase("82%", 0.82f)]
        [TestCase("46%", 0.46f)]
        [TestCase("0%", 0f)]
        public void ParsesPercent(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParsePercent(text, out var ratio));
            Assert.AreEqual(expected, ratio, 0.0001f);
        }

        [TestCase("82")]
        [TestCase("82px")]
        [TestCase("%")]
        [TestCase("")]
        public void RejectsNonPercent(string text)
        {
            Assert.IsFalse(UssTokenValue.TryParsePercent(text, out _));
        }

        [TestCase("117deg", 117f)]
        [TestCase("-30deg", -30f)]
        [TestCase("0deg", 0f)]
        public void ParsesDegrees(string text, float expected)
        {
            Assert.IsTrue(UssTokenValue.TryParseDegrees(text, out var degrees));
            Assert.AreEqual(expected, degrees, 0.0001f);
        }

        [TestCase("117")]
        [TestCase("deg")]
        [TestCase("1.2rad")]
        [TestCase("")]
        public void RejectsNonDegrees(string text)
        {
            Assert.IsFalse(UssTokenValue.TryParseDegrees(text, out _));
        }
    }
}
