import { describe, it, expect } from "vitest";
import { clamp01 } from "./clamp01";

describe("clamp01", () => {
  it("負値は 0 に丸める", () => {
    expect(clamp01(-0.5)).toBe(0);
  });
  it("1 超は 1 に丸める", () => {
    expect(clamp01(2)).toBe(1);
  });
  it("NaN は 0 にする", () => {
    expect(clamp01(NaN)).toBe(0);
  });
  it("範囲内はそのまま返す", () => {
    expect(clamp01(0.4)).toBe(0.4);
  });
});
