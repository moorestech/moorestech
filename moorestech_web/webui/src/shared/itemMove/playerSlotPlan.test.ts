import { describe, it, expect } from "vitest";
import { GRAB, planPlayerLeftClick, planPlayerRightClick, planPlayerDoubleClick } from "./playerSlotPlan";
import type { PlayerSlotContext } from "./playerSlotPlan";
import type { PlayerInventoryData } from "@/bridge";

const slot = (itemId: number, count: number) => ({ itemId, count });
const inv = (grabCount: number): PlayerInventoryData => ({
  mainSlots: [slot(1, 98), slot(0, 0)],
  grab: grabCount > 0 ? slot(9, grabCount) : slot(0, 0),
  equipment: [],
  selectedEquipment: -1,
  equipmentSelectionConfirmationRevision: 0,
});
const ctx = (grabCount: number, blockItemSlots: { itemId: number; count: number }[] | null): PlayerSlotContext => ({
  inventory: inv(grabCount),
  maxStack: 100,
  blockItemSlots,
});

describe("planPlayerLeftClick", () => {
  it("grab保持中の空スロットはスプリットドラッグ開始を返す", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(4, null))).toEqual({ kind: "beginSplitDrag" });
  });
  it("grab保持中は grab 全量をクリックスロットへ置く", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 98), false, ctx(4, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 0 }, count: 4 } }],
    });
  });
  it("grab保持中の中身ありは同ID・別IDとも全量置きへ回す（別IDはサーバーがswap）", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(9, 2), false, ctx(4, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 0 }, count: 4 } }],
    });
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 2), false, ctx(4, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 0 }, count: 4 } }],
    });
  });
  it("空手+空スロットは無操作", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(0, null))).toEqual({ kind: "actions", actions: [] });
  });
  it("空手+中身ありは全量を grab へ拾う", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 98), false, ctx(0, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: GRAB, count: 98 } }],
    });
  });
  it("Shift+クリックはブロック開時 block へ配分する", () => {
    const blockSlots = [slot(1, 99), slot(0, 0)];
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, blockSlots))).toEqual({
      kind: "actions",
      actions: [
        { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 0 }, count: 1 } },
        { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 1 }, count: 4 } },
      ],
    });
  });
  it("Shift+クリックはブロック閉時、main からは配分先を持たず無操作になる（旧main⇔hotbar振り分けは廃止済み）", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, null))).toEqual({ kind: "actions", actions: [] });
  });
  it("equipment からの Shift は持ち物本体(main)へ戻す", () => {
    expect(planPlayerLeftClick({ area: "equipment", slot: 0 }, slot(1, 1), true, ctx(0, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: { area: "equipment", slot: 0 }, to: { area: "main", slot: 0 }, count: 1 } }],
    });
  });
  // grab 起点は Shift 分岐へ到達しない。grab 保持中は全量置き/スプリットドラッグ開始が先に返り、空手なら中身が無く無操作になる
  // A grab origin never reaches the Shift branch: holding a grab returns place-all/beginSplitDrag first, and empty-handed means an empty slot
  it("grab 起点の Shift は配分先を持たず、掴み状態に応じて全量置きか無操作になる", () => {
    expect(planPlayerLeftClick(GRAB, slot(9, 4), true, ctx(4, null))).toEqual({
      kind: "actions",
      actions: [{ type: "inventory.move_item", payload: { from: GRAB, to: GRAB, count: 4 } }],
    });
    expect(planPlayerLeftClick(GRAB, slot(0, 0), true, ctx(0, null))).toEqual({ kind: "actions", actions: [] });
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
    expect(planPlayerDoubleClick({ area: "main", slot: 2 })).toEqual([
      { type: "inventory.collect", payload: { slot: { area: "main", slot: 2 } } },
    ]);
  });
});
