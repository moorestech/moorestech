import { describe, expect, it } from "vitest";
import { computePowerRate, splitSlotIndices, fuelRatio, stopReasonText } from "./detailLogic";

describe("detailLogic", () => {
  it("computePowerRate follows the uGUI formula", () => {
    expect(computePowerRate(50, 100)).toBe(0.5);
    // RequestPower==0 は uGUI と同じく 1.0 扱い
    // RequestPower==0 counts as 1.0, same as uGUI
    expect(computePowerRate(0, 0)).toBe(1);
  });
  it("splitSlotIndices splits input→output→module in order", () => {
    expect(splitSlotIndices({ input: 2, output: 1, module: 1 }, 4)).toEqual({
      input: [0, 1], output: [2], module: [3],
    });
    // 総数不一致でも範囲外を作らない
    // Never produce out-of-range indices even when counts mismatch
    expect(splitSlotIndices({ input: 3, output: 2, module: 0 }, 4)).toEqual({
      input: [0, 1, 2], output: [3], module: [],
    });
  });
  it("fuelRatio clamps to 0..1 and handles zero denominators", () => {
    expect(fuelRatio(5, 10)).toBe(0.5);
    expect(fuelRatio(0, 0)).toBe(0);
    expect(fuelRatio(20, 10)).toBe(1);
  });
  it("stopReasonText maps reasons to uGUI wording", () => {
    expect(stopReasonText("none")).toBe("");
    expect(stopReasonText("rocked")).toBe("ロック");
    expect(stopReasonText("overRequirePower")).toBe("パワー不足");
  });
});
