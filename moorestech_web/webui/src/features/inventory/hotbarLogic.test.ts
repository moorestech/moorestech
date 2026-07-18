import { describe, it, expect } from "vitest";
import { keyToHotbarIndex, cycleHotbar, accumulateHotbarWheel } from "./hotbarLogic";

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

describe("accumulateHotbarWheel", () => {
  it("小さい入力は累積し閾値を越えるまで切り替えない", () => {
    expect(accumulateHotbarWheel(0, 40)).toEqual({ remainder: 0.4, steps: 0 });
    expect(accumulateHotbarWheel(0.4, 70)).toEqual({ remainder: 0.10000000000000009, steps: 1 });
  });

  it("標準1ノッチ(±100)でちょうど1段切り替わる", () => {
    expect(accumulateHotbarWheel(0, 100)).toEqual({ remainder: 0, steps: 1 });
    expect(accumulateHotbarWheel(0, -100)).toEqual({ remainder: 0, steps: -1 });
  });

  it("大きい負入力は複数段を返して端数を残す", () => {
    expect(accumulateHotbarWheel(0, -250)).toEqual({ remainder: -0.5, steps: -2 });
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
