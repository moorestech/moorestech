import { describe, expect, it } from "vitest";
import { activeCategoryAtScroll, isJumpSettled, trailingSpacerHeight } from "./buildMenuScrollSpy";

const offsets = [
  { categoryGuid: "a", top: 0 },
  { categoryGuid: "b", top: 300 },
  { categoryGuid: "c", top: 720 },
];

describe("activeCategoryAtScroll", () => {
  it("先頭より上は先頭カテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 0)).toBe("a");
  });
  it("見出しの間は直前の見出しのカテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 299)).toBe("a");
    expect(activeCategoryAtScroll(offsets, 500)).toBe("b");
  });
  it("見出し上端ちょうど（±1px）はその見出しのカテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 300)).toBe("b");
    expect(activeCategoryAtScroll(offsets, 719)).toBe("c");
  });
  it("末尾を越えても末尾カテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 5000)).toBe("c");
  });
  it("見出しが無ければnull", () => {
    expect(activeCategoryAtScroll([], 10)).toBeNull();
  });
});

describe("isJumpSettled", () => {
  it("目標±1px以内で到達", () => {
    expect(isJumpSettled(299.4, 300)).toBe(true);
    expect(isJumpSettled(301, 300)).toBe(true);
  });
  it("それ以上離れていれば未到達", () => {
    expect(isJumpSettled(297, 300)).toBe(false);
  });
});

describe("trailingSpacerHeight", () => {
  it("末尾群が視口より短ければ差分を返す", () => {
    expect(trailingSpacerHeight(600, 220)).toBe(380);
  });
  it("末尾群が視口以上なら0", () => {
    expect(trailingSpacerHeight(600, 600)).toBe(0);
    expect(trailingSpacerHeight(600, 900)).toBe(0);
  });
});
