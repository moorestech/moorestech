// 進捗値を 0..1 に丸める。NaN は 0 として扱う（uGUI scrollbar.size と同じ範囲）。
// Clamp the progress value to 0..1; NaN is treated as 0 (same range as uGUI scrollbar.size).
export function clampProgress(n: number): number {
  if (Number.isNaN(n)) return 0;
  if (n < 0) return 0;
  if (n > 1) return 1;
  return n;
}

// 丸めた進捗を CSS 幅パーセント文字列へ変換（0.4 → "40%"）。
// Convert the clamped progress into a CSS width percent string (0.4 → "40%").
export function percentWidth(n: number): string {
  return `${clampProgress(n) * 100}%`;
}
