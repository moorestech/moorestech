// 判定はDOM非依存(scroll-spy等)
// DOM-free judgments (scroll-spy etc.), called from the hook

export type CategoryHeadingOffset = { categoryGuid: string; top: number };

// スムーズスクロールの停止位置は小数で揺れるため±1pxを同値とみなす(ジャンプ到達判定専用)
// Smooth scrolling settles on fractional positions, so treat ±1px as equal (jump-arrival check only)
const jumpSettleTolerancePx = 1;

// scroll-spyが見出しを活性化するしきい値。ジャンプ到達判定とは別軸のため独立して調整できる
// Threshold at which scroll-spy activates a heading; kept independent from the jump-arrival tolerance so either can be tuned alone
const scrollSpyActivationTolerancePx = 1;

// 許容内で最後の見出しが現在地
// The last heading within tolerance of the top is current
export function activeCategoryAtScroll(offsets: CategoryHeadingOffset[], scrollTop: number): string | null {
  if (offsets.length === 0) return null;
  // 視口上端以上にある見出しのうちtopが最大のものを選ぶ。配列順ではなくtopの大小で決めるので昇順でない入力でも視口に正しく追従する
  // Among headings at or above the viewport top, pick the one with the largest top; deciding by magnitude rather than array order keeps non-ascending input tracking the viewport correctly
  let reached: CategoryHeadingOffset | null = null;
  // 1件もreachedが無い場合の既定は先頭(最小top)の見出し
  // When nothing is reached yet, default to the topmost (smallest-top) heading
  let topmost = offsets[0];
  for (const offset of offsets) {
    if (offset.top - scrollSpyActivationTolerancePx <= scrollTop) {
      if (reached === null || offset.top > reached.top) reached = offset;
    }
    if (offset.top < topmost.top) topmost = offset;
  }
  return (reached ?? topmost).categoryGuid;
}

export function isJumpSettled(scrollTop: number, targetTop: number): boolean {
  return Math.abs(scrollTop - targetTop) <= jumpSettleTolerancePx;
}

// 末尾カテゴリの見出しを視口上端まで持ち上げられるよう不足分を埋める
// Fill the shortfall so the last category heading can still reach the viewport top
export function trailingSpacerHeight(viewportHeight: number, lastGroupHeight: number): number {
  return Math.max(0, viewportHeight - lastGroupHeight);
}
