// スロットの量バッジ表記を1つに揃える。N0形式（千区切り・小数なし）で uGUI の amount.ToString("N0") を踏襲
// The single spelling for slot amount badges: N0 style (thousands-separated, no fraction), mirroring uGUI's amount.ToString("N0")
export function formatSlotAmount(n: number): string {
  return n.toLocaleString("en-US", { maximumFractionDigits: 0 });
}
