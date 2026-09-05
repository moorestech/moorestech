import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import {
  computePowerRate,
  splitSlotIndices,
  fuelRatio,
  itemsPerMinute,
  machineStateDisplay,
  pumpSectionDisplay,
  stopReasonTranslationKey,
} from "./detailLogic";

describe("detailLogic", () => {
  it("computes output items per minute from recipe seconds", () => {
    expect(itemsPerMinute(3, 15)).toBe(12);
    expect(itemsPerMinute(3, 0)).toBeNull();
  });
  it("computePowerRate follows the uGUI formula", () => {
    expect(computePowerRate(50, 100)).toBe(0.5);
    // RequestPower==0 は uGUI と同じく 1.0 扱い
    // RequestPower==0 counts as 1.0, same as uGUI
    expect(computePowerRate(0, 0)).toBe(1);
  });
  it("splitSlotIndices splits input→output→module in order", () => {
    expect(splitSlotIndices({ input: 2, output: 1, module: 1 }, 4)).toEqual({
      input: [0, 1], output: [2], module: [3],
    });
    // 総数不一致でも範囲外を作らない
    // Never produce out-of-range indices even when counts mismatch
    expect(splitSlotIndices({ input: 3, output: 2, module: 0 }, 4)).toEqual({
      input: [0, 1, 2], output: [3], module: [],
    });
  });
  it("fuelRatio clamps to 0..1 and handles zero denominators", () => {
    expect(fuelRatio(5, 10)).toBe(0.5);
    expect(fuelRatio(0, 0)).toBe(0);
    expect(fuelRatio(20, 10)).toBe(1);
  });
  it("stopReasonTranslationKey maps finite reasons to typed localization keys", () => {
    expect(stopReasonTranslationKey("none")).toBeNull();
    expect(stopReasonTranslationKey("rocked")).toBe(L.ui.blockInventory.stopReasonLocked);
    expect(stopReasonTranslationKey("overRequirePower")).toBe(
      L.ui.blockInventory.stopReasonInsufficientPower,
    );
  });

  describe("machineStateDisplay", () => {
    // ラベル・不足トーン・充足率の表示可否を1枚のテーブルで確定する（判別子はstateのみ）
    // One table settles label, insufficient tone, and rate visibility, keyed solely by the state
    it.each([
      { state: "idle", key: L.ui.blockInventory.machineStateIdle, insufficient: false, showPowerRate: true },
      { state: "processing", key: L.ui.blockInventory.machineStateProcessing, insufficient: false, showPowerRate: true },
      { state: "halted", key: L.ui.blockInventory.machineStateHalted, insufficient: true, showPowerRate: false },
    ] as const)("$state の表示を確定する", ({ state, key, insufficient, showPowerRate }) => {
      expect(machineStateDisplay(state)).toEqual({ labelKey: key, insufficient, showPowerRate });
    });

    // 要求電力0で稼働する機械（石窯・ボイラー）は停止中と同一表示に潰さない
    // Machines that operate at zero request power (kiln, boiler) must not collapse into the halted display
    it("要求電力0でも稼働状態なら充足率を表示する", () => {
      expect(machineStateDisplay("idle").showPowerRate).toBe(true);
      expect(machineStateDisplay("processing").showPowerRate).toBe(true);
    });
  });

  // 表示分岐を純関数化しDOM無しで固定
  // Pure-function display branch pinned without a DOM
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
});
