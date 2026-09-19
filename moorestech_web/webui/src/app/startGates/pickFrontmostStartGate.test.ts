// 順序の正本はC#のprecedence。Web側は比べるだけで、並び順の知識を持たない
// C#'s precedence owns the order; the Web side only compares and holds no knowledge of the sequence
import { describe, expect, it } from "vitest";
import { pickFrontmostStartGate } from "./useFrontmostStartGate";

describe("pickFrontmostStartGate", () => {
  it("待っていなければ何も選ばない", () => {
    expect(pickFrontmostStartGate({ eventLanguage: { waiting: false, precedence: 0 } })).toBeNull();
    expect(pickFrontmostStartGate({ eventLanguage: null })).toBeNull();
  });

  it("待っている1枚を選ぶ", () => {
    expect(pickFrontmostStartGate({ eventLanguage: { waiting: true, precedence: 0 } })).toBe("eventLanguage");
  });
});
