// 720p で 12px 以下の和文が潰れるため、文字寸法トークンは 13px 未満を 13px へ上げる（S7・plan.md §3.10）
// Japanese at 12px or less crushes at 720p, so text-size tokens under 13px are raised to 13px (S7, plan.md §3.10)
export const minimumFontSizePx = 13;

// アイコン上の個数は縁取りで読ませるので下限の外に置く（plan.md §3.10）
// Counts drawn over icons rely on an outline, so they stay outside the floor (plan.md §3.10)
const exemptNames = new Set(["--fluid-amount-font-size", "--inventory-count-base-font-size"]);

export function isFontSizeToken(name) {
  return name.endsWith("-font-size") || name.endsWith("-text-size");
}

export function applyFontSizeFloor(name, pxValue) {
  if (!isFontSizeToken(name) || exemptNames.has(name) || pxValue >= minimumFontSizePx) return { px: pxValue, raised: false };
  return { px: minimumFontSizePx, raised: true };
}
