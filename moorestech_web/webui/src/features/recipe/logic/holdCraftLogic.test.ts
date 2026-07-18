import { describe, it, expect } from "vitest";
import { advanceHoldCraft, holdCraftProgress, MAX_FRAME_DELTA_SECONDS } from "./holdCraftLogic";

describe("advanceHoldCraft", () => {
  it("craftTime 未到達なら経過を積み増すだけ", () => {
    expect(advanceHoldCraft(0, 0.3, 1)).toEqual({ elapsed: 0.3, didCraft: false });
  });
  it("craftTime 到達でクラフト発火し経過を0へ戻す", () => {
    expect(advanceHoldCraft(0.8, 0.3, 1)).toEqual({ elapsed: 0, didCraft: true });
  });
  it("ちょうど craftTime でも発火する", () => {
    expect(advanceHoldCraft(0.5, 0.5, 1)).toEqual({ elapsed: 0, didCraft: true });
  });
  it("連続クラフト: 発火直後は0から積み増しが再開する", () => {
    const first = advanceHoldCraft(0.9, 0.2, 1);
    expect(first.didCraft).toBe(true);
    const second = advanceHoldCraft(first.elapsed, 0.2, 1);
    expect(second).toEqual({ elapsed: 0.2, didCraft: false });
  });
  it("巨大 delta でも1tick最大1クラフト", () => {
    const step = advanceHoldCraft(0, 100, 1);
    expect(step.didCraft).toBe(true);
    expect(step.elapsed).toBe(0);
  });
});

describe("holdCraftProgress", () => {
  it("経過/craftTime を 0..1 で返す", () => {
    expect(holdCraftProgress(0.5, 2)).toBe(0.25);
  });
  it("craftTime 超過は 1 でクランプ", () => {
    expect(holdCraftProgress(3, 2)).toBe(1);
  });
  it("craftTime<=0 は満杯", () => {
    expect(holdCraftProgress(0, 0)).toBe(1);
  });
});

describe("MAX_FRAME_DELTA_SECONDS", () => {
  it("正の上限値である", () => {
    expect(MAX_FRAME_DELTA_SECONDS).toBeGreaterThan(0);
  });
});
