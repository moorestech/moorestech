using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// 候補窓座標への換算を固定（L0）
    /// Pins the candidate window conversion (L0)
    /// </summary>
    public class ImeCaretScreenPositionTest
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void ScalesPanelPointToFullHdScreen()
        {
            // 720p→1080pで1.5倍
            // 720p to 1080p scales by 1.5
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(400, 300), new Vector2(1280, 720), new Vector2(1920, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(600, 450), screen);
        }

        [Test]
        public void ScalesUltraWideExpandPanelUniformly()
        {
            // 21:9は倍率x・yとも1.5
            // 21:9 scales 1.5 on both axes
            var panelSize = new Vector2(2560f / 1.5f, 720);
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(1000, 300), panelSize, new Vector2(2560, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(1500, 450), screen);
        }

        [Test]
        public void UsesEachAxisRatioInsteadOfSharedMinimum()
        {
            // 軸ごとの比を使う
            // Use the per-axis ratio
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(640, 360), new Vector2(1280, 720), new Vector2(2560, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(1280, 540), screen);
        }

        [Test]
        public void KeepsTopLeftOriginWithoutFlippingY()
        {
            // 左上原点で返す
            // Returns top-left origin
            var panelSize = new Vector2(1280, 720);
            var screenSize = new Vector2(1920, 1080);
            AssertNear(new Vector2(0, 0), ImeCaretScreenPosition.FromPanel(new Vector2(0, 0), panelSize, screenSize, ImeCursorOrigin.TopLeft));
            AssertNear(new Vector2(1920, 1080), ImeCaretScreenPosition.FromPanel(new Vector2(1280, 720), panelSize, screenSize, ImeCursorOrigin.TopLeft));
            AssertNear(new Vector2(0, 150), ImeCaretScreenPosition.FromPanel(new Vector2(0, 100), panelSize, screenSize, ImeCursorOrigin.TopLeft));
        }

        [Test]
        public void FlipsYForBottomLeftOrigin()
        {
            // Mac は左下原点で読むので、行の下端（上から 100）は下から数え直す
            // Mac reads bottom-left origin, so a line bottom 100 from the top is re-measured from the bottom
            var panelSize = new Vector2(1280, 720);
            var screenSize = new Vector2(1920, 1080);
            AssertNear(new Vector2(0, 1080), ImeCaretScreenPosition.FromPanel(new Vector2(0, 0), panelSize, screenSize, ImeCursorOrigin.BottomLeft));
            AssertNear(new Vector2(1920, 0), ImeCaretScreenPosition.FromPanel(new Vector2(1280, 720), panelSize, screenSize, ImeCursorOrigin.BottomLeft));
            AssertNear(new Vector2(600, 930), ImeCaretScreenPosition.FromPanel(new Vector2(400, 100), panelSize, screenSize, ImeCursorOrigin.BottomLeft));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, $"x: expected {expected} actual {actual}");
            Assert.AreEqual(expected.y, actual.y, Tolerance, $"y: expected {expected} actual {actual}");
        }
    }
}
