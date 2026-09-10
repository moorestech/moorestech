// レシピ液体量は小数を取るため丸め切らない。一方この関数は実行時タンク残量(double)も通るので、
// 流量加算が生む 0.3333333333333333 のような値が枠を溢れないよう桁は有限で止める
// Recipe fluid amounts are fractional so they must survive, yet runtime tank amounts (double) pass here too:
// cap the digits so a flow-accumulated value like 0.3333333333333333 never overruns the cell
const MAX_FRACTION_DIGITS = 2;

// スロットの量バッジ表記を1つに揃える。整数は千区切り、小数は表示桁まで残す
// The single spelling for slot amount badges: thousands separators for integers, fractions kept to the display digits
export function formatSlotAmount(n: number): string {
  return n.toLocaleString("en-US", { maximumFractionDigits: MAX_FRACTION_DIGITS });
}
