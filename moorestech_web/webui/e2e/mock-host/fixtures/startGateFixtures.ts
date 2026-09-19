// 開始ゲートの答えさせる順。本物はC#の起動順が配るprecedenceで、mockは同じ値（言語0）を返す
// The start gates' answer order; the real host ships precedence from the C# boot order and the mock returns the same value (language 0)
export const StartGatePrecedence = {
  eventLanguage: 0,
} as const;
