// indexを配列長の有効範囲へ丸める
// Clamp an index to the valid range for an array length
export function clampIndex(index: number, length: number): number {
  return Math.max(0, Math.min(index, length - 1));
}
