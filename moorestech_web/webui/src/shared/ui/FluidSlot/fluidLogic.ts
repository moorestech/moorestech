import { clamp01 } from "@/shared/clamp01";

// 量バッジのN0整形はスロット共通の1関数が持つ。液体側は従来の呼び名で公開し続ける
// The N0 badge formatting lives in the one shared slot function; fluids keep exposing it under their existing name
export { formatSlotAmount as formatAmount } from "../slotAmountFormat";

// amount/capacity を 0..1 の充填率へ。capacity<=0 は 0、超過は 1 にクランプ
// Convert amount/capacity into a 0..1 fill ratio; capacity<=0 yields 0 and overflow clamps to 1
export function fillRatio(amount: number, capacity: number): number {
  if (capacity <= 0) return 0;
  return clamp01(amount / capacity);
}
