import { describe, it, expect } from "vitest";
import { clampProgress, percentValue } from "./progressLogic";

describe("clampProgress", () => {
  it("負値は 0 に丸める", () => {
    expect(clampProgress(-0.5)).toBe(0);
  });
  it("1 超は 1 に丸める", () => {
    expect(clampProgress(2)).toBe(1);
  });
  it("NaN は 0 にする", () => {
    expect(clampProgress(NaN)).toBe(0);
  });
  it("範囲内はそのまま返す", () => {
    expect(clampProgress(0.4)).toBe(0.4);
  });
});

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
