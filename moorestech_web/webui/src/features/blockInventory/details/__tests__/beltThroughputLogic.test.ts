import { describe, expect, it } from "vitest";
import type { BeltThroughputWindowData } from "@/bridge";
import { beltRecentSecondBars, beltThroughputRowDisplay, cycleBeltWindow, selectBeltWindow } from "../beltThroughputLogic";

const windows: BeltThroughputWindowData[] = [
  { ticks: 90, measured: true, exitCount: 4, maxCount: 4.5 },
  { ticks: 180, measured: true, exitCount: 9, maxCount: 9 },
  { ticks: 360, measured: false, exitCount: 0, maxCount: 18 },
];

describe("beltThroughputLogic", () => {
  it("selects the window by length and falls back to the first", () => {
    expect(selectBeltWindow(windows, 180)?.ticks).toBe(180);
    expect(selectBeltWindow(windows, 5760)?.ticks).toBe(90);
    expect(selectBeltWindow([], 180)).toBeUndefined();
  });

  // 端で止まり、一覧に無い長さは先頭扱い
  // Stops at the ends; an unlisted length counts as the first
  it("cycles one window at a time without wrapping", () => {
    expect(cycleBeltWindow(windows, 180, 1)).toBe(360);
    expect(cycleBeltWindow(windows, 360, 1)).toBe(360);
    expect(cycleBeltWindow(windows, 180, -1)).toBe(90);
    expect(cycleBeltWindow(windows, 90, -1)).toBe(90);
    expect(cycleBeltWindow(windows, 5760, 1)).toBe(180);
    expect(cycleBeltWindow([], 180, 1)).toBe(180);
  });

  // 180tick=9秒に9個で100%、分換算は9×1200/180=60
  // 9 items over 180 ticks (9 s) is 100%, per minute 9×1200/180 = 60
  it("formats a filled window with percent and per-minute", () => {
    const row = beltThroughputRowDisplay(windows[1]);
    expect(row.key).toBe("ui.blockInventory.beltThroughputWindow");
    expect(row.params).toEqual({ seconds: "9", count: 9, max: "9", percent: 100 });
    expect(row.itemsPerMinute).toBe("60.0");
  });

  // 90tickは4.5秒、最大4.5個。4個で89%
  // 90 ticks is 4.5 s with a maximum of 4.5; 4 items is 89%
  it("keeps fractional seconds and maximums to one decimal", () => {
    const row = beltThroughputRowDisplay(windows[0]);
    expect(row.params).toEqual({ seconds: "4.5", count: 4, max: "4.5", percent: 89 });
    expect(row.itemsPerMinute).toBe("53.3");
  });

  it("reports a measuring window without a per-minute figure", () => {
    const row = beltThroughputRowDisplay(windows[2]);
    expect(row.key).toBe("ui.blockInventory.beltThroughputMeasuring");
    expect(row.params).toEqual({ seconds: "18" });
    expect(row.itemsPerMinute).toBeNull();
  });

  // 停止中のベルト(最大0)は0%
  // A stopped belt (maximum 0) shows 0%
  it("shows zero percent for a stopped belt", () => {
    const row = beltThroughputRowDisplay({ ticks: 180, measured: true, exitCount: 0, maxCount: 0 });
    expect(row.params).toEqual({ seconds: "9", count: 0, max: "0", percent: 0 });
  });

  // 足りない秒は先頭側をnullで埋め、最大を超えた秒は1に丸め、停止中は0
  // Missing seconds are null at the front, a second above the maximum rounds to 1, a stopped belt gives 0
  it("maps per-second counts onto bar heights oldest first", () => {
    expect(beltRecentSecondBars([1, 0, 2], 2, 5)).toEqual([null, null, 0.5, 0, 1]);
    expect(beltRecentSecondBars([3, 1], 2, 2)).toEqual([1, 0.5]);
    expect(beltRecentSecondBars([1, 1, 1], 2, 2)).toEqual([0.5, 0.5]);
    expect(beltRecentSecondBars([1], 0, 2)).toEqual([null, 0]);
    expect(beltRecentSecondBars([], 2, 3)).toEqual([null, null, null]);
  });
});
