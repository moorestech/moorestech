import { describe, it, expect } from "vitest";
import { formatAmount, fillRatio } from "./fluidLogic";

describe("formatAmount", () => {
  it("0 はそのまま", () => {
    expect(formatAmount(0)).toBe("0");
  });
  it("千未満は区切らない", () => {
    expect(formatAmount(500)).toBe("500");
  });
  it("1000 は千区切りになる", () => {
    expect(formatAmount(1000)).toBe("1,000");
  });
  it("百万超も区切る", () => {
    expect(formatAmount(1234567)).toBe("1,234,567");
  });
});

describe("fillRatio", () => {
  it("半量は 0.5", () => {
    expect(fillRatio(500, 1000)).toBe(0.5);
  });
  it("capacity 0 は 0", () => {
    expect(fillRatio(500, 0)).toBe(0);
  });
  it("超過は 1 にクランプ", () => {
    expect(fillRatio(1500, 1000)).toBe(1);
  });
});
