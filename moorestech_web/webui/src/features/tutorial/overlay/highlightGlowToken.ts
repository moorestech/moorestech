let cachedHighlightGlowPx: number | null = null;

// クリップ計算はJSで行うため、固定長トークンをCSS変数から読み取り単一の値源を保つ
// The clip math runs in JS, so read the fixed-length token from the CSS variable to keep one source
// トークンは実行中に変わらないため初回読み取りをキャッシュし、毎レンダーのgetComputedStyleを避ける
// The token never changes at runtime, so cache the first read and avoid per-render getComputedStyle
export function readTutorialHighlightGlowPx(): number {
  if (cachedHighlightGlowPx !== null) return cachedHighlightGlowPx;
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--tutorial-highlight-glow");
  const parsedGlowPx = Number.parseFloat(raw);
  if (!Number.isFinite(parsedGlowPx) || parsedGlowPx <= 0) {
    throw new Error("--tutorial-highlight-glow must be a positive CSS length");
  }
  cachedHighlightGlowPx = parsedGlowPx;
  return cachedHighlightGlowPx;
}
