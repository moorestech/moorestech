import { describe, it, expect } from "vitest";
import { GRAB, planPlayerLeftClick, planPlayerRightClick, planPlayerDoubleClick } from "./playerSlotPlan";
import type { PlayerSlotContext } from "./playerSlotPlan";
import type { PlayerInventoryData } from "@/bridge";

const slot = (itemId: number, count: number) => ({ itemId, count });
const inv = (grabCount: number): PlayerInventoryData => ({
  mainSlots: [slot(1, 98), slot(0, 0)],
  hotbarSlots: [slot(0, 0)],
  grab: grabCount > 0 ? slot(9, grabCount) : slot(0, 0),
  selectedHotbar: 0,
});
const ctx = (grabCount: number, blockItemSlots: { itemId: number; count: number }[] | null): PlayerSlotContext => ({
  inventory: inv(grabCount),
  maxStack: 100,
  blockItemSlots,
});

describe("planPlayerLeftClick", () => {
  it("grab保持中は grab 全量をクリックスロットへ置く", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(4, null))).toEqual([
      { type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 1 }, count: 4 } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(0, null))).toEqual([]);
  });
  it("空手+中身ありは全量を grab へ拾う", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 98), false, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: GRAB, count: 98 } },
    ]);
  });
  it("Shift+クリックはブロック開時 block へ配分する", () => {
    const blockSlots = [slot(1, 99), slot(0, 0)];
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, blockSlots))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 0 }, count: 1 } },
      { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 1 }, count: 4 } },
    ]);
  });
  it("Shift+クリックはブロック閉時に反対エリア（main→hotbar）へ配分する", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "hotbar", slot: 0 }, count: 5 } },
    ]);
  });
  it("hotbar からの Shift は main へ向かう", () => {
    expect(planPlayerLeftClick({ area: "hotbar", slot: 0 }, slot(1, 1), true, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "hotbar", slot: 0 }, to: { area: "main", slot: 0 }, count: 1 } },
    ]);
  });
});

describe("planPlayerRightClick", () => {
  it("grab保持中はクリックスロットへ1個置く", () => {
    expect(planPlayerRightClick({ area: "main", slot: 1 }, slot(0, 0), 4)).toEqual([
      { type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 1 }, count: 1 } },
    ]);
  });
  it("空手+中身ありは inventory.split（半分掴みはホスト計算）", () => {
    expect(planPlayerRightClick({ area: "main", slot: 0 }, slot(1, 7), 0)).toEqual([
      { type: "inventory.split", payload: { from: { area: "main", slot: 0 } } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planPlayerRightClick({ area: "main", slot: 1 }, slot(0, 0), 0)).toEqual([]);
  });
});

describe("planPlayerDoubleClick", () => {
  it("クリックスロットを送るだけ（収集先はホストが grab 状態で決める）", () => {
    expect(planPlayerDoubleClick({ area: "hotbar", slot: 2 })).toEqual([
      { type: "inventory.collect", payload: { slot: { area: "hotbar", slot: 2 } } },
    ]);
  });
});
