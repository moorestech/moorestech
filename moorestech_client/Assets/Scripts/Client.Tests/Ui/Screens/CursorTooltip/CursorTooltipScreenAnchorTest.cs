using Client.Ui.Screens.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ロック中は中央、解除中はポインタを指す（左下原点の実画面 px。L0）
    /// The production anchor: centre while locked, the pointer while unlocked (both bottom-left screen px; L0)
    /// </summary>
    public class CursorTooltipScreenAnchorTest
    {
        [Test]
        public void 解除中はポインタをそのまま指す()
        {
            var anchor = CursorTooltipScreenAnchor.AnchorOnScreen(false, new Vector2(100f, 620f), new Vector2(640f, 360f));
            Assert.AreEqual(new Vector2(100f, 620f), anchor);
        }

        [Test]
        public void ロック中はポインタを使わず画面中央を指す()
        {
            var anchor = CursorTooltipScreenAnchor.AnchorOnScreen(true, new Vector2(1240f, 40f), new Vector2(640f, 300f));
            Assert.AreEqual(new Vector2(640f, 300f), anchor);
        }
    }
}
