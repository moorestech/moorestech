using System;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// ハイライトのラベル面の置き場所。Web の HighlightLabel の配置の写し
    /// Where the highlight label face goes; a copy of Web's HighlightLabel placement
    /// </summary>
    public static class TutorialHighlightLabelPlacement
    {
        public static TutorialLabelPosition Place(TutorialClipRect box, TutorialClipRect clip, float gap, float faceWidth, float faceHeight)
        {
            // 既定は枠の下。下に収まらず上に収まる時だけ上へ反転する（ユーザー裁定 2026-08-22）
            // Below the ring by default; flip above only when it does not fit below and does fit above (user ruling 2026-08-22)
            var fitsBelow = box.Bottom + gap + faceHeight <= clip.Bottom;
            var fitsAbove = clip.Top <= box.Top - gap - faceHeight;
            var flipped = !fitsBelow && fitsAbove;
            var top = flipped ? box.Top - gap - faceHeight : box.Bottom + gap;
            // 横は反転先が無いので右端で押し戻し、器より広ければ左端に揃える
            // Horizontally there is no flip side, so push back from the right and settle at the left when wider
            var left = Math.Max(clip.Left, Math.Min(box.Left, clip.Right - faceWidth));
            return new TutorialLabelPosition(left, top);
        }

        // アンカー自身がクリップで欠けていればラベルを出さない（Web renderOutline）
        // Skip the label while the anchor itself is partly clipped (Web renderOutline)
        public static bool ShowsLabel(TutorialClipRect anchorRect, TutorialClipRect clip)
        {
            return clip.ContainsRect(anchorRect);
        }
    }

    public readonly struct TutorialLabelPosition
    {
        public readonly float Left;
        public readonly float Top;

        public TutorialLabelPosition(float left, float top)
        {
            Left = left;
            Top = top;
        }
    }
}
