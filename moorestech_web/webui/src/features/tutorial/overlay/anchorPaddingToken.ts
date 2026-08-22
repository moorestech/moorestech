let publishedPaddingPx: number | null = null;

// 逃げ量の正本はマスタのpaddingPx。CSSの初期値を下回る値では書き換えず、上回った時だけ広げる
// The master's paddingPx is the authority; only a value above the CSS initial widens the variable
// 縮めないのは、逃げがスクロール領域の高さを変えるため。段数によるバーの出方が実行中に揺れるのを防ぐ
// It never shrinks because the clearance changes the scroller's height, which would make the bar flicker between row counts
export function publishTutorialAnchorPaddingPx(paddingPx: number): void {
  const root = document.documentElement;
  if (publishedPaddingPx === null) {
    const initial = Number.parseFloat(getComputedStyle(root).getPropertyValue("--tutorial-anchor-padding"));
    publishedPaddingPx = Number.isFinite(initial) ? initial : 0;
  }
  if (paddingPx <= publishedPaddingPx) return;
  publishedPaddingPx = paddingPx;
  root.style.setProperty("--tutorial-anchor-padding", `${paddingPx}px`);
}

