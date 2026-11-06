using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 1 本スクロールの判定（Web buildMenuScrollSpy.ts の写し）。視口に依存しない
    /// Judgements for the single scroll (port of the Web buildMenuScrollSpy.ts); viewport-free
    /// </summary>
    public static class BuildMenuScrollSpy
    {
        // スムーズスクロールの停止位置は小数で揺れるので ±1px を同値とみなす（ジャンプ到達専用）
        // Smooth scrolling settles on fractional positions, so ±1px counts as equal (jump arrival only)
        private const float JumpSettleTolerancePx = 1f;

        // scroll-spy が見出しを活性にするしきい値。到達判定とは別に調整できるよう分ける
        // The scroll-spy activation threshold, kept separate from the arrival tolerance so each can be tuned
        private const float ScrollSpyActivationTolerancePx = 1f;

        // 視口上端以上にある見出しのうち上端が最大のもの。無ければ最も上の見出し
        // Among headings at or above the viewport top, the one with the largest top; otherwise the topmost heading
        public static Guid? ActiveCategoryAtScroll(IReadOnlyList<CategoryHeadingOffset> offsets, float scrollTop)
        {
            if (offsets.Count == 0) return null;
            CategoryHeadingOffset? reached = null;
            var topmost = offsets[0];
            foreach (var offset in offsets)
            {
                var isReached = offset.Top - ScrollSpyActivationTolerancePx <= scrollTop;
                if (isReached && (reached == null || reached.Value.Top < offset.Top)) reached = offset;
                if (offset.Top < topmost.Top) topmost = offset;
            }
            return (reached ?? topmost).CategoryGuid;
        }

        public static bool IsJumpSettled(float scrollTop, float targetTop)
        {
            return Math.Abs(scrollTop - targetTop) <= JumpSettleTolerancePx;
        }

        // 末尾カテゴリの見出しを視口上端まで持ち上げられるよう、足りない分を埋める
        // Fill the shortfall so the last category heading can still reach the viewport top
        public static float TrailingSpacerHeight(float viewportHeight, float lastGroupHeight)
        {
            return Math.Max(0f, viewportHeight - lastGroupHeight);
        }
    }
}
