// 開始ゲート3枚の答えさせる順。本物はC#の起動順が配るprecedenceで、mockは同じ値（言語0・同意1・前回異常終了2）を返す
// The start gates' answer order; the real host ships precedence from the C# boot order and the mock returns the same values (language 0, consent 1, crash 2)
export const StartGatePrecedence = {
  eventLanguage: 0,
  consent: 1,
  crashReport: 2,
} as const;
