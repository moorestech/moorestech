using Client.Ui.Foundation.Tooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// ツールチップの置き方を確かめる（L0）
    /// Verifies tooltip placement (L0)
    /// </summary>
    public class HoverTooltipPlacementTest
    {
        private static readonly Rect Bounds = new(0f, 0f, 1280f, 720f);

        [Test]
        public void 対象の上に中央をそろえ間隔を空けて置く()
        {
            var position = HoverTooltipPlacement.Place(new Rect(600f, 300f, 48f, 48f), new Vector2(100f, 30f), Bounds, 5f, 8f);
            Assert.AreEqual(new Vector2(574f, 265f), position);
        }

        [Test]
        public void 上に入らなければ下へ反転する()
        {
            var position = HoverTooltipPlacement.Place(new Rect(600f, 10f, 48f, 48f), new Vector2(100f, 30f), Bounds, 5f, 8f);
            Assert.AreEqual(new Vector2(574f, 63f), position);
        }

        [Test]
        public void 横にはみ出すと枠から余白の分だけ内へ押し戻す()
        {
            // Mantine の shift({ padding: 8 })。左端は 8、右端は 1280 - 8 - 100 = 1172
            // Mantine shift({ padding: 8 }); the left edge is 8 and the right edge is 1280 - 8 - 100 = 1172
            Assert.AreEqual(8f, HoverTooltipPlacement.Place(new Rect(0f, 300f, 48f, 48f), new Vector2(100f, 30f), Bounds, 5f, 8f).x);
            Assert.AreEqual(1172f, HoverTooltipPlacement.Place(new Rect(1250f, 300f, 30f, 48f), new Vector2(100f, 30f), Bounds, 5f, 8f).x);
        }

        [Test]
        public void 余白を除いた枠より広い顔は左の余白にそろえる()
        {
            Assert.AreEqual(8f, HoverTooltipPlacement.Place(new Rect(600f, 300f, 48f, 48f), new Vector2(1270f, 30f), Bounds, 5f, 8f).x);
        }
    }
}
