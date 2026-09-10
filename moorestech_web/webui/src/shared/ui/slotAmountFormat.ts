// レシピ液体量は小数を取り得るため、丸めずに残せる上限桁を使う（Intlの最大値）
// Recipe fluid amounts can be fractional, so use the largest digit count Intl allows and never round them away
const MAX_FRACTION_DIGITS = 20;

// スロットの量バッジ表記を1つに揃える。整数は千区切り、小数は実量のまま出す
// The single spelling for slot amount badges: thousands separators for integers, fractions kept as the real amount
export function formatSlotAmount(n: number): string {
  return n.toLocaleString("en-US", { maximumFractionDigits: MAX_FRACTION_DIGITS });
}
