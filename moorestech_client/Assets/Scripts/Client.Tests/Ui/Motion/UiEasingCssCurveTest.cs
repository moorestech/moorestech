using Client.Ui.Foundation.Motion.Tween;
using NUnit.Framework;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// UiEasing の CSS の ease-out・ease-in（cubic-bezier）の値がブラウザの定義と同じことを確かめる（L0。UITK だけ）
    /// Verifies the CSS ease-out and ease-in (cubic-bezier) of UiEasing match the browser definitions (L0; UITK-only)
    /// </summary>
    public class UiEasingCssCurveTest
    {
        private const float Tolerance = 0.001f;

        [TestCase(0.25f, 0.3781f)]
        [TestCase(0.5f, 0.6846f)]
        [TestCase(0.75f, 0.9065f)]
        public void CssEaseOutはcubic_bezier_0_0_058_1の値を返す(float progress, float expected)
        {
            Assert.AreEqual(expected, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, progress), Tolerance);
        }

        [TestCase(0.25f, 0.0935f)]
        [TestCase(0.5f, 0.3154f)]
        [TestCase(0.75f, 0.6219f)]
        public void CssEaseInはcubic_bezier_042_0_1_1の値を返す(float progress, float expected)
        {
            Assert.AreEqual(expected, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, progress), Tolerance);
        }

        [Test]
        public void 端は0と1に留まる()
        {
            Assert.AreEqual(0f, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, 0f));
            Assert.AreEqual(0f, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, 0f));
            Assert.AreEqual(1f, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, 1f));
            Assert.AreEqual(1f, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, 1f));
        }
    }
}
