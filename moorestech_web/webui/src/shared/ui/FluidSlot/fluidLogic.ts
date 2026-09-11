import { clamp01 } from "@/shared/clamp01";

// amount/capacity を 0..1 の充填率へ。capacity<=0 は 0、超過は 1 にクランプ
// Convert amount/capacity into a 0..1 fill ratio; capacity<=0 yields 0 and overflow clamps to 1
export function fillRatio(amount: number, capacity: number): number {
  if (capacity <= 0) return 0;
  return clamp01(amount / capacity);
}
