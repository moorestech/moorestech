using Client.Ui.Screens.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面が指す点: ロック中は中央・左下原点を左上原点へ反転（L0）
    /// The production anchor: centre while locked, bottom-left origin flipped to top-left (L0)
    /// </summary>
    public class CursorTooltipScreenAnchorTest
    {
        [Test]
        public void 解除中はポインタを上下反転した左上原点で指す()
        {
            var anchor = CursorTooltipScreenAnchor.TopLeftScreenPosition(false, new Vector2(100f, 620f), new Vector2(640f, 360f), 720f);
            Assert.AreEqual(new Vector2(100f, 100f), anchor);
        }

        [Test]
        public void ロック中はポインタを使わず画面中央を指す()
        {
            var anchor = CursorTooltipScreenAnchor.TopLeftScreenPosition(true, new Vector2(1240f, 40f), new Vector2(640f, 300f), 720f);
            Assert.AreEqual(new Vector2(640f, 420f), anchor);
        }
    }
}
