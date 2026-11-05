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
    }
}
