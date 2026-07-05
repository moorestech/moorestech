import { clamp01 } from "@/shared/clamp01";

// 丸めた進捗を Mantine Progress の value へ変換（0.4 → 40）。0..1 の丸めは共有 clamp01 に一本化。
// Convert the clamped progress into the Mantine Progress value (0.4 → 40); the 0..1 clamp is single-sourced in clamp01.
export function percentValue(n: number): number {
  return clamp01(n) * 100;
}
