import { describe, expect, it } from "vitest";
import { clampIndex } from "./clampIndex";

describe("clampIndex", () => {
  it("length 内に収める", () => {
    expect(clampIndex(5, 3)).toBe(2);
  });

  it("0 未満にしない", () => {
    expect(clampIndex(0, 0)).toBe(0);
  });
});
