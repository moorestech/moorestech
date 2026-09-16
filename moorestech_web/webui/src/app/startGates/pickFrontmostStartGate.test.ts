// 順序の正本はC#のprecedence。Web側は比べるだけで、並び順の知識を持たない
// C#'s precedence owns the order; the Web side only compares and holds no knowledge of the sequence
import { describe, expect, it } from "vitest";
import { pickFrontmostStartGate } from "./useFrontmostStartGate";

const closed = { waiting: false, precedence: 0 };

describe("pickFrontmostStartGate", () => {
  it("どれも待っていなければ何も選ばない", () => {
    expect(pickFrontmostStartGate({ eventLanguage: closed, consent: null, crashReport: closed })).toBeNull();
  });

  it("待っている1枚を選ぶ", () => {
    expect(pickFrontmostStartGate({ eventLanguage: closed, consent: closed, crashReport: { waiting: true, precedence: 2 } }))
      .toBe("crashReport");
  });

  it("同時に待てば precedence の小さい1枚だけを選ぶ", () => {
    expect(pickFrontmostStartGate({
      eventLanguage: { waiting: true, precedence: 0 },
      consent: { waiting: true, precedence: 1 },
      crashReport: { waiting: true, precedence: 2 },
    })).toBe("eventLanguage");
  });

  // 並び順をWeb側で決め打ちしていないことの証明。C#が順序を入れ替えればそのまま従う
  // Proves the Web side does not hard-code the order: if C# swaps it, the choice follows
  it("C#が順序を入れ替えればそれに従う", () => {
    expect(pickFrontmostStartGate({
      eventLanguage: { waiting: true, precedence: 5 },
      consent: { waiting: true, precedence: 1 },
      crashReport: { waiting: true, precedence: 0 },
    })).toBe("crashReport");
  });
});
