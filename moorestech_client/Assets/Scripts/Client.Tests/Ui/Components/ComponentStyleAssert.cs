using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components
{
    /// <summary>
    /// 色照合。1/255 未満は同色
    /// Color comparison; under 1/255 is the same color
    /// </summary>
    public static class ComponentStyleAssert
    {
        private const float Tolerance = 1f / 255f;

        public static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, Tolerance, $"{message} r");
            Assert.AreEqual(expected.g, actual.g, Tolerance, $"{message} g");
            Assert.AreEqual(expected.b, actual.b, Tolerance, $"{message} b");
            Assert.AreEqual(expected.a, actual.a, Tolerance, $"{message} a");
        }

        public static Color Hex(string html)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(html, out var color), $"not a colour: {html}");
            return color;
        }

        public static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }
    }
}
