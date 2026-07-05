// 数値を 0..1 に丸める共有ユーティリティ。NaN は 0 扱い（進捗系 UI で共用）
// Shared 0..1 clamp; NaN treated as 0 (shared across progress UIs)
export function clamp01(n: number): number {
  if (Number.isNaN(n)) return 0;
  if (n < 0) return 0;
  if (n > 1) return 1;
  return n;
}
