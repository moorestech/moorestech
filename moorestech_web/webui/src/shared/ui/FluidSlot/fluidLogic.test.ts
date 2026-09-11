import { describe, it, expect } from "vitest";
import { fillRatio } from "./fluidLogic";

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
