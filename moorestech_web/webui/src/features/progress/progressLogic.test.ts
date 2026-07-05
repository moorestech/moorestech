import { describe, it, expect } from "vitest";
import { percentValue } from "./progressLogic";

// Mantine Progress の value（0..100 の数値）への変換を検証する
// Verifies conversion into the Mantine Progress value (a 0..100 number)
describe("percentValue", () => {
  it("0.4 は 40 になる", () => {
    expect(percentValue(0.4)).toBe(40);
  });
  it("1 は 100 になる", () => {
    expect(percentValue(1)).toBe(100);
  });
  it("範囲外と NaN はクランプされる", () => {
    expect(percentValue(-1)).toBe(0);
    expect(percentValue(2)).toBe(100);
    expect(percentValue(Number.NaN)).toBe(0);
  });
});
