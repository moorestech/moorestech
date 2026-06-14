import { describe, it, expect } from "vitest";
import { keyToHotbarIndex, cycleHotbar } from "./hotbarLogic";

describe("keyToHotbarIndex", () => {
  it('"1" を 0 に変換する', () => {
    expect(keyToHotbarIndex("1")).toBe(0);
  });
  it('"9" を 8 に変換する', () => {
    expect(keyToHotbarIndex("9")).toBe(8);
  });
  it('"0" は範囲外で null', () => {
    expect(keyToHotbarIndex("0")).toBeNull();
  });
  it("数字以外は null", () => {
    expect(keyToHotbarIndex("a")).toBeNull();
  });
});

describe("cycleHotbar", () => {
  it("末尾を超えたら 0 へ折り返す", () => {
    expect(cycleHotbar(8, 1, 9)).toBe(0);
  });
  it("0 未満は末尾(count-1)へ折り返す", () => {
    expect(cycleHotbar(0, -1, 9)).toBe(8);
  });
  it("通常の前進", () => {
    expect(cycleHotbar(3, 1, 9)).toBe(4);
  });
});
