// ドラッグスクロールの純粋計算。DOMに触れないためvitestで直接検証できる
// Pure math for drag-scroll; no DOM access so it is directly unit-testable in vitest

// タップとドラッグを分ける移動量の閾値。5px未満はクリック選択として扱う
// Movement threshold separating tap from drag; under 5px stays a click-selection
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
