// scroll-spy・ジャンプ到達・末尾スペーサの判定。DOMを持たずフックから呼ばれる
// Scroll-spy, jump-settled, and trailing-spacer math; DOM-free, called from the hook

export type CategoryHeadingOffset = { categoryGuid: string; top: number };

// スムーズスクロールの停止位置は小数で揺れるため±1pxを同値とみなす
// Smooth scrolling settles on fractional positions, so treat ±1px as equal
export const scrollSettleTolerancePx = 1;

// 視口上端（許容内）以上にある最後の見出しが現在地。先頭より上なら先頭
// The last heading at or above the viewport top (within tolerance) is current; above the first means the first
export function activeCategoryAtScroll(offsets: CategoryHeadingOffset[], scrollTop: number): string | null {
  if (offsets.length === 0) return null;
  let active: string | null = null;
  for (let i = 0; i < offsets.length; i++) {
    const offset = offsets[i];
    const isLastHeading = i === offsets.length - 1;
    // 末尾の見出しだけtolerance適用。その他は完全一致
    // Apply tolerance only to last heading; others require exact match
    const threshold = isLastHeading ? offset.top - scrollSettleTolerancePx : offset.top;
    if (threshold <= scrollTop) {
      active = offset.categoryGuid;
    }
  }
  return active;
}

export function isJumpSettled(scrollTop: number, targetTop: number): boolean {
  return Math.abs(scrollTop - targetTop) <= scrollSettleTolerancePx;
}

// 末尾カテゴリの見出しを視口上端まで持ち上げられるよう不足分を埋める
// Fill the shortfall so the last category heading can still reach the viewport top
export function trailingSpacerHeight(viewportHeight: number, lastGroupHeight: number): number {
  return Math.max(0, viewportHeight - lastGroupHeight);
}
