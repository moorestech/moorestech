using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components
{
    /// <summary>
    /// 部品 L1 の色の照合。USS の色は 8bit なので 1/255 未満の差を同じとみなす
    /// Color comparison for component L1; USS colors are 8-bit, so differences under 1/255 count as equal
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

        public static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }
    }
}
