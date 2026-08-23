let cachedLabelGapPx: number | null = null;

// ラベルの反転判定と描画位置はどちらもJSで決めるため、隙間もCSS変数から読み取り単一の値源を保つ
// Both the flip test and the placement run in JS, so read the gap from the CSS variable to keep one source
// トークンは実行中に変わらないため初回読み取りをキャッシュし、毎レンダーのgetComputedStyleを避ける
// The token never changes at runtime, so cache the first read and avoid per-render getComputedStyle
export function readTutorialHighlightLabelGapPx(): number {
  if (cachedLabelGapPx !== null) return cachedLabelGapPx;
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--tutorial-highlight-label-gap");
  const parsedGapPx = Number.parseFloat(raw);
  if (!Number.isFinite(parsedGapPx) || parsedGapPx <= 0) {
    throw new Error("--tutorial-highlight-label-gap must be a positive CSS length");
  }
  cachedLabelGapPx = parsedGapPx;
  return cachedLabelGapPx;
}
