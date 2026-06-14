import { describe, it, expect } from "vitest";
import { clampProgress, percentWidth } from "./progressLogic";

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

describe("percentWidth", () => {
  it("0.4 を \"40%\" にする", () => {
    expect(percentWidth(0.4)).toBe("40%");
  });
  it("1 を \"100%\" にする", () => {
    expect(percentWidth(1)).toBe("100%");
  });
  it("負値は \"0%\" に丸める", () => {
    expect(percentWidth(-0.5)).toBe("0%");
  });
});
