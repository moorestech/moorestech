import { describe, it, expect } from "vitest";
import { keyToHotbarIndex } from "./hotbarLogic";

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
