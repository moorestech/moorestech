using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// キャレットのパネル座標から候補窓のスクリーン座標への換算を固定する（L0）
    /// Pins the conversion from a caret's panel point to the candidate window's screen point (L0)
    /// </summary>
    public class ImeCaretScreenPositionTest
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void ScalesPanelPointToFullHdScreen()
        {
            // S9 の Windows の形: 1280×720 のパネルを 1920×1080 に出すと 1.5 倍
            // The S9 Windows case: a 1280x720 panel shown on 1920x1080 is scaled by 1.5
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(400, 300), new Vector2(1280, 720), new Vector2(1920, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(600, 450), screen);
        }

        [Test]
        public void ScalesUltraWideExpandPanelUniformly()
        {
            // 21:9 の 2560×1080 では Expand でパネルが 1706.67×720 に広がり、倍率は x・y とも 1.5
            // On a 21:9 2560x1080 screen Expand widens the panel to 1706.67x720, so both axes scale by 1.5
            var panelSize = new Vector2(2560f / 1.5f, 720);
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(1000, 300), panelSize, new Vector2(2560, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(1500, 450), screen);
        }

        [Test]
        public void UsesEachAxisRatioInsteadOfSharedMinimum()
        {
            // 縦横比の違うパネルでも、共通の min 倍率ではなく軸ごとの比を使う
            // Even when the aspect ratios differ, each axis uses its own ratio rather than a shared minimum
            var screen = ImeCaretScreenPosition.FromPanel(new Vector2(640, 360), new Vector2(1280, 720), new Vector2(2560, 1080), ImeCursorOrigin.TopLeft);
            AssertNear(new Vector2(1280, 540), screen);
        }

        [Test]
        public void KeepsTopLeftOriginWithoutFlippingY()
        {
            // 返り値は左上原点。パネルの上端は 0、下端はスクリーンの高さ
            // The result is top-left origin: the panel top maps to 0 and the bottom to the screen height
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
