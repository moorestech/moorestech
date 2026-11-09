using System;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 枠に当てる祖先クリップの inset 計算。Web の clipPathInset の写し
    /// Computes the ancestor-clip inset applied to the ring; a copy of Web's clipPathInset
    /// </summary>
    public static class TutorialHighlightClip
    {
        // 枠に当てる inset を返す。false は描かない（Web の clipPathInset の null）
        // Returns the inset applied to the ring; false means skip rendering (Web clipPathInset returning null)
        public static bool TryComputeInset(TutorialClipRect box, TutorialClipRect clip, float outset, out TutorialClipInset inset)
        {
            inset = default;
            // silent-ok: クリップで見えない枠は Web と同じく出さない仕様（失敗ではない）/ L1 TutorialOverlayClipTest が見る
            // クランプ後の値ではグロー分の帯を見逃すので、素の矩形で先に交わりを見る
            // Clamped values can miss a fully clipped box, so check raw disjointness first
            if (box.IsDisjoint(clip)) return false;
            var top = Math.Max(-outset, clip.Top - box.Top);
            var right = Math.Max(-outset, box.Right - clip.Right);
            var bottom = Math.Max(-outset, box.Bottom - clip.Bottom);
            var left = Math.Max(-outset, clip.Left - box.Left);
            // silent-ok: 切った後に面積が残らない枠は Web と同じく出さない仕様 / L1 TutorialOverlayClipTest が見る
            if (box.Height <= top + bottom || box.Width <= left + right) return false;
            inset = new TutorialClipInset(top, right, bottom, left);
            return true;
        }
    }

    public readonly struct TutorialClipInset
    {
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Left;

        public TutorialClipInset(float top, float right, float bottom, float left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }
    }
}
