// 液体量を N0 形式（千区切り・小数なし）へ整形。uGUI の amount.ToString("N0") を踏襲
// Format the fluid amount as N0 style (thousands-separated, no fraction); mirrors uGUI amount.ToString("N0")
export function formatAmount(n: number): string {
  return n.toLocaleString("en-US", { maximumFractionDigits: 0 });
}

// amount/capacity を 0..1 の充填率へ。capacity<=0 は 0、超過は 1 にクランプ
// Convert amount/capacity into a 0..1 fill ratio; capacity<=0 yields 0 and overflow clamps to 1
export function fillRatio(amount: number, capacity: number): number {
  if (capacity <= 0) return 0;
  const ratio = amount / capacity;
  if (ratio < 0) return 0;
  if (ratio > 1) return 1;
  return ratio;
}
