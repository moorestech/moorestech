using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// 部品の L1 で解決後の色をトークンの値と比べる。USS の色は 8bit で丸まるので 0.01 まで許す
    /// Compares resolved colours with token values in component L1 tests; USS colours round to 8 bits, so allow 0.01
    /// </summary>
    public static class ControlStyleAssert
    {
        private const float ChannelTolerance = 0.01f;

        public static void AssertColor(string html, Color actual)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(html, out var expected), $"not a colour: {html}");
            AssertColor(expected, actual);
        }

        public static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, ChannelTolerance, $"r: expected {expected} got {actual}");
            Assert.AreEqual(expected.g, actual.g, ChannelTolerance, $"g: expected {expected} got {actual}");
            Assert.AreEqual(expected.b, actual.b, ChannelTolerance, $"b: expected {expected} got {actual}");
            Assert.AreEqual(expected.a, actual.a, ChannelTolerance, $"a: expected {expected} got {actual}");
        }
    }
}
