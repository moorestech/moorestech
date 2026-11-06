using Client.Ui.State.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// ポインタの右下 12px に置き、画面の端では内側へ押し戻す（L0。Web の tooltipPosition.ts）
    /// Places 12px below-right of the pointer and pushes back inside at the edges (L0; Web tooltipPosition.ts)
    /// </summary>
    public class CursorTooltipPlacementTest
    {
        /// <web-test file="src/shared/tooltip/tooltipPosition.test.ts">keeps the tooltip inside the viewport</web-test>
        [Test]
        public void 画面の右下の端では内側へ押し戻す()
        {
            var placed = CursorTooltipPlacement.Clamp(new Vector2(790, 590), new Vector2(180, 80), new Vector2(800, 600));
            Assert.AreEqual(new Vector2(608, 508), placed);
        }

        [Test]
        public void 余白があればポインタの右下12pxに置き左上の端では12pxに留める()
        {
            Assert.AreEqual(new Vector2(652, 372), CursorTooltipPlacement.Clamp(new Vector2(640, 360), new Vector2(120, 40), new Vector2(1280, 720)));
            Assert.AreEqual(new Vector2(12, 12), CursorTooltipPlacement.Clamp(new Vector2(-50, -50), new Vector2(120, 40), new Vector2(1280, 720)));
        }

        [Test]
        public void パネル単位で受けても余白は実画面の12pxになる()
        {
            // 2560x1080 は Expand で 1.5 倍。パネルの 640,360 は実画面の 960,540
            // 2560x1080 is 1.5x under Expand; panel 640,360 is screen 960,540
            var placed = CursorTooltipPlacement.PlaceInPanel(new Vector2(640, 360), new Vector2(120, 40), new Vector2(1706.6666f, 720), 1.5f);
            Assert.AreEqual(640f + 12f / 1.5f, placed.x, 0.001f);
            Assert.AreEqual(360f + 12f / 1.5f, placed.y, 0.001f);
        }
    }
}
