import { describe, expect, it } from "vitest";
import { DRAG_THRESHOLD_PX, exceededThreshold, nextScrollTop } from "./dragScrollMath";

describe("exceededThreshold", () => {
  // 閾値未満はタップ扱い、以上でドラッグ確定
  // Below the threshold stays a tap; at or above commits to a drag
  it("閾値未満はfalse", () => {
    expect(exceededThreshold(3, 3)).toBe(false);
  });
  it("閾値ちょうどはtrue", () => {
    expect(exceededThreshold(DRAG_THRESHOLD_PX, 0)).toBe(true);
  });
  it("斜め移動もhypotで判定する", () => {
    expect(exceededThreshold(4, 4)).toBe(true);
  });
});

describe("nextScrollTop", () => {
  // ポインタを上へ動かすとscrollTopが増え、下へ動かすと減る
  // Moving the pointer up increases scrollTop; moving down decreases it
  it("上へドラッグでscrollTop増加", () => {
    expect(nextScrollTop(100, 200, 170)).toBe(130);
  });
  it("下へドラッグでscrollTop減少", () => {
    expect(nextScrollTop(100, 200, 240)).toBe(60);
  });
});
