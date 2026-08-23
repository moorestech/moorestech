let cachedClipInsetPx: number | null = null;

// クリップ境界を広げた容器は座標計算がその分ずれる。JS側もCSS変数から読み単一の値源を保つ
// A container that widened its clip edge shifts its coordinate math by that much, so JS reads the same CSS variables
// カスタムプロパティは代入値のまま返るため、calcを持つ --tutorial-anchor-clip-inset は読めない。
// tokens.css と同じ内訳（padding + glow）を素の長さトークンから足し直す
// A custom property comes back as its substitution value, so the calc in --tutorial-anchor-clip-inset cannot be parsed;
// re-add the same breakdown tokens.css uses (padding + glow) from the plain length tokens
// トークンは実行中に変わらないため初回読み取りをキャッシュし、毎レンダーのgetComputedStyleを避ける
// The tokens never change at runtime, so cache the first read and avoid per-render getComputedStyle
export function readTutorialAnchorClipInsetPx(): number {
  if (cachedClipInsetPx !== null) return cachedClipInsetPx;
  const root = getComputedStyle(document.documentElement);
  const readPx = (name: string) => {
    const parsed = Number.parseFloat(root.getPropertyValue(name));
    if (!Number.isFinite(parsed) || parsed <= 0) throw new Error(`${name} must be a positive CSS length`);
    return parsed;
  };
  cachedClipInsetPx = readPx("--tutorial-anchor-padding") + readPx("--tutorial-highlight-glow");
  return cachedClipInsetPx;
}
