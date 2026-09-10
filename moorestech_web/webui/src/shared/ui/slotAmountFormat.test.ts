import { describe, it, expect } from "vitest";
import { formatSlotAmount } from "./slotAmountFormat";

describe("formatSlotAmount", () => {
  it("0 はそのまま", () => {
    expect(formatSlotAmount(0)).toBe("0");
  });
  it("千未満は区切らない", () => {
    expect(formatSlotAmount(500)).toBe("500");
  });
  it("1000 は千区切りになる", () => {
    expect(formatSlotAmount(1000)).toBe("1,000");
  });
  it("百万超も区切る", () => {
    expect(formatSlotAmount(1234567)).toBe("1,234,567");
  });

  // レシピ液体量は小数を取る。丸めると実量と異なる値をバッジが主張する
  // Recipe fluid amounts are fractional, and rounding would make the badge claim an amount that is not the real one
  it("小数は丸めずそのまま出す", () => {
    expect(formatSlotAmount(0.1)).toBe("0.1");
    expect(formatSlotAmount(1.4)).toBe("1.4");
  });
  it("千区切りと小数は同時に効く", () => {
    expect(formatSlotAmount(1234.5)).toBe("1,234.5");
  });
});
