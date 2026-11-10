using Client.Ui.Components.Recipe;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行の四隅の括弧と外周の輪の矩形（Web の ::before/::after と box-shadow の spread）
    /// Rectangles for the recipe row's corner brackets and outer ring (the Web ::before/::after and box-shadow spread)
    /// </summary>
    public class RecipeRowFrameGeometryTest
    {
        [Test]
        public void 括弧は枠の四隅に長さ9太さ1の横と縦の棒が2本ずつ()
        {
            // 枠の箱は Web の擬似要素と同じく padding の箱から 3px 外。横 200・縦 50 の枠で見る
            // The frame box sits 3px outside the padding box like the Web pseudo-elements; checked on a 200 by 50 frame
            var rects = new Rect[RecipeRowFrameGeometry.BracketRectCount];
            RecipeRowFrameGeometry.WriteBracketRects(new Vector2(200f, 50f), 9f, 1f, rects);
            Assert.AreEqual(new Rect(0f, 0f, 9f, 1f), rects[0]);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 9f), rects[1]);
            Assert.AreEqual(new Rect(191f, 0f, 9f, 1f), rects[2]);
            Assert.AreEqual(new Rect(199f, 0f, 1f, 9f), rects[3]);
            Assert.AreEqual(new Rect(0f, 49f, 9f, 1f), rects[4]);
            Assert.AreEqual(new Rect(0f, 41f, 1f, 9f), rects[5]);
            Assert.AreEqual(new Rect(191f, 49f, 9f, 1f), rects[6]);
            Assert.AreEqual(new Rect(199f, 41f, 1f, 9f), rects[7]);
        }

        [Test]
        public void 輪は線の箱の外側を幅1点2の帯4本で囲み線の箱とは重ならない()
        {
            // 線は 1px なので線の箱は枠の 2px 内側（196 × 46）。輪はその外 1.2px
            // The border is 1px, so the border box is 2px inside the frame (196 x 46); the ring is 1.2px outside it
            var rects = new Rect[RecipeRowFrameGeometry.RingRectCount];
            RecipeRowFrameGeometry.WriteRingRects(new Vector2(200f, 50f), 2f, 1.2f, rects);
            AssertRect(new Rect(0.8f, 0.8f, 198.4f, 1.2f), rects[0], "top");
            AssertRect(new Rect(0.8f, 48f, 198.4f, 1.2f), rects[1], "bottom");
            AssertRect(new Rect(0.8f, 2f, 1.2f, 46f), rects[2], "left");
            AssertRect(new Rect(198f, 2f, 1.2f, 46f), rects[3], "right");
        }

        private static void AssertRect(Rect expected, Rect actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f, $"{what} x");
            Assert.AreEqual(expected.y, actual.y, 0.001f, $"{what} y");
            Assert.AreEqual(expected.width, actual.width, 0.001f, $"{what} width");
            Assert.AreEqual(expected.height, actual.height, 0.001f, $"{what} height");
        }
    }
}
