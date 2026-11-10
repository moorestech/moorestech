using UnityEngine;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行の外周の輪と四隅の括弧の矩形。座標は枠の箱（padding の箱から外へ広げた箱）の左上が原点
    /// Rectangles for the recipe row's outer ring and corner brackets, in frame-box space (the padding box grown outward) with the top-left origin
    /// </summary>
    internal static class RecipeRowFrameGeometry
    {
        public const int RingRectCount = 4;
        public const int BracketRectCount = 8;

        public static void WriteRingRects(Vector2 frameSize, float borderInset, float ringWidth, Rect[] rects)
        {
            // Web の box-shadow 0 0 0 spread は線の箱を spread だけ広げた輪。上下は角まで、左右は線の箱の高さだけ
            // The Web box-shadow 0 0 0 spread is the border box grown by the spread; top and bottom reach the corners, sides span the border box height
            var border = new Rect(borderInset, borderInset, frameSize.x - borderInset * 2f, frameSize.y - borderInset * 2f);
            rects[0] = new Rect(border.xMin - ringWidth, border.yMin - ringWidth, border.width + ringWidth * 2f, ringWidth);
            rects[1] = new Rect(border.xMin - ringWidth, border.yMax, border.width + ringWidth * 2f, ringWidth);
            rects[2] = new Rect(border.xMin - ringWidth, border.yMin, ringWidth, border.height);
            rects[3] = new Rect(border.xMax, border.yMin, ringWidth, border.height);
        }

        public static void WriteBracketRects(Vector2 frameSize, float length, float thickness, Rect[] rects)
        {
            // Web の ::before が上の 2 隅、::after が下の 2 隅。各隅に横の棒（length × thickness）と縦の棒（thickness × length）
            // The Web ::before draws the top corners and ::after the bottom ones; each corner has a horizontal bar and a vertical bar
            var right = frameSize.x;
            var bottom = frameSize.y;
            rects[0] = new Rect(0f, 0f, length, thickness);
            rects[1] = new Rect(0f, 0f, thickness, length);
            rects[2] = new Rect(right - length, 0f, length, thickness);
            rects[3] = new Rect(right - thickness, 0f, thickness, length);
            rects[4] = new Rect(0f, bottom - thickness, length, thickness);
            rects[5] = new Rect(0f, bottom - length, thickness, length);
            rects[6] = new Rect(right - length, bottom - thickness, length, thickness);
            rects[7] = new Rect(right - thickness, bottom - length, thickness, length);
        }
    }
}
