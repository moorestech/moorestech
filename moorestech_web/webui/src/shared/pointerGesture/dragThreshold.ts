// ポインタジェスチャの純粋計算
// Shared pointer-gesture math and DOM probing; the math touches no DOM so it is directly unit-testable in vitest

// タップとドラッグを分ける閾値
// Movement threshold separating tap from drag; under 5px stays a tap/selection
export const DRAG_THRESHOLD_PX = 5;

// 押下点からの移動が閾値を超えたらドラッグ確定
// A move past the threshold from the press point commits to a drag
export function exceededThreshold(dx: number, dy: number): boolean {
  return Math.hypot(dx, dy) >= DRAG_THRESHOLD_PX;
}

// 掴んだ位置を基準に、ポインタを下へ動かすと内容も下へ流れる自然なパン量
// Natural pan: relative to the grabbed point, moving the pointer down slides the content down
export function nextScrollTop(startScrollTop: number, startY: number, currentY: number): number {
  return startScrollTop - (currentY - startY);
}

// 対象が要素なら返す。closestを持つかで判定しinstanceofのグローバル依存を避ける
// Return the target when it is an element; probe for closest to avoid a global instanceof dependency
export function asElement(target: EventTarget | null): HTMLElement | null {
  return target && typeof (target as HTMLElement).closest === "function" ? (target as HTMLElement) : null;
}
