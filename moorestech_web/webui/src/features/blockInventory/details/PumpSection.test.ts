import { describe, expect, it } from "vitest";
import { pumpSectionDisplay } from "./PumpSection";

// 表示分岐は純関数に切り出し、警告行の出し分けをDOM無しで固定する
// The display branch is a pure function so the warning toggle is pinned without a DOM
describe("pumpSectionDisplay", () => {
  it("汲み上げ中流体が空なら警告行だけを出す", () => {
    const display = pumpSectionDisplay({ pumpingFluids: [] });
    expect(display.showNoVein).toBe(true);
    expect(display.showPumpingFluids).toBe(false);
  });

  it("汲み上げ中流体があれば流体行を出し警告行は出さない", () => {
    const display = pumpSectionDisplay({ pumpingFluids: [{ fluidId: 1, fluidGuid: "54000000-0000-4000-8000-000000000001", amountPerMinute: 150 }] });
    expect(display.showNoVein).toBe(false);
    expect(display.showPumpingFluids).toBe(true);
  });
});
