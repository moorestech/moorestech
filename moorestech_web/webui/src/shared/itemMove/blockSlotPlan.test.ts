import { describe, it, expect } from "vitest";
import { planBlockLeftClick, planBlockRightClick, planBlockDoubleClick } from "./blockSlotPlan";
import type { BlockSlotContext } from "./blockSlotPlan";

const slot = (itemId: number, count: number) => ({ itemId, count });
const ctx = (grabCount: number, mainSlots = [slot(0, 0)]): BlockSlotContext => ({
  grabCount,
  maxStack: 100,
  mainSlots,
});

describe("planBlockLeftClick", () => {
  it("grab保持中は grab 全量を block スロットへ置く（空スロットでも置く）", () => {
    expect(planBlockLeftClick(1, slot(0, 0), false, ctx(4))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 1 }, count: 4 } },
    ]);
  });
  it("空手+中身ありは全量を grab へ拾う", () => {
    expect(planBlockLeftClick(2, slot(10, 6), false, ctx(0))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 2 }, to: { area: "grab", slot: 0 }, count: 6 } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planBlockLeftClick(3, slot(0, 0), false, ctx(0))).toEqual([]);
  });
  it("Shift+クリックは main の同種スタック→空きの順に配分する", () => {
    const mainSlots = [slot(1, 98), slot(0, 0)];
    expect(planBlockLeftClick(4, slot(1, 7), true, ctx(0, mainSlots))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 4 }, to: { area: "main", slot: 0 }, count: 2 } },
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 4 }, to: { area: "main", slot: 1 }, count: 5 } },
    ]);
  });
  it("grab保持中の Shift は通常の置きと同じ（uGUI同様 grab が優先）", () => {
    expect(planBlockLeftClick(0, slot(1, 7), true, ctx(4))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 0 }, count: 4 } },
    ]);
  });
});

describe("planBlockRightClick", () => {
  it("grab保持中は block スロットへ1個置く", () => {
    expect(planBlockRightClick(2, slot(0, 0), 5)).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 2 }, count: 1 } },
    ]);
  });
  it("空手+2個以上は半分(切り捨て)を grab へ拾う", () => {
    expect(planBlockRightClick(0, slot(1, 7), 0)).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 3 } },
    ]);
  });
  it("空手+1個は半分が0のため無操作(uGUI準拠)", () => {
    expect(planBlockRightClick(0, slot(1, 1), 0)).toEqual([]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planBlockRightClick(0, slot(0, 0), 0)).toEqual([]);
  });
});

describe("planBlockDoubleClick", () => {
  it("クリックスロットを送るだけ（収集先はホストが grab 状態で決める）", () => {
    expect(planBlockDoubleClick(1)).toEqual([
      { type: "block_inventory.collect", payload: { slot: { area: "block", slot: 1 } } },
    ]);
  });
});
