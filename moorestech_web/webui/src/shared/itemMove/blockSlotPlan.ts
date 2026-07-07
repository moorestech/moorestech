import type { SlotData } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "./planDirectMoves";
import type { PlannedAction } from "./plannedAction";

// ブロックスロット操作の判定材料。mainSlots は Shift 配分の宛先（uGUI はサブ→メインのみでホットバー除外）
// Inputs for block-slot decisions; mainSlots is the Shift target (uGUI moves sub→main only, hotbar excluded)
export type BlockSlotContext = {
  grabCount: number;
  maxStack: number | undefined;
  mainSlots: SlotData[];
};

const grabRef = { area: "grab", slot: 0 } as const;
const blockRef = (slot: number) => ({ area: "block", slot }) as const;

// 左クリック: Shift(空手+中身あり)は main へ配分 / grab保持なら全量置き / 中身ありなら全量掴み
// Left click: Shift (empty-handed, filled) allocates into main / place all while holding grab / pick the whole stack
export function planBlockLeftClick(index: number, slot: SlotData, shiftKey: boolean, ctx: BlockSlotContext): PlannedAction[] {
  if (shiftKey && ctx.grabCount === 0 && slot.count > 0) {
    return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, ctx.mainSlots).map((m) => ({
      type: "block_inventory.move_item",
      payload: { from: blockRef(index), to: { area: "main", slot: m.slot }, count: m.count },
    }));
  }
  if (ctx.grabCount > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: grabRef, to: blockRef(index), count: ctx.grabCount } }];
  }
  if (slot.itemId > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: blockRef(index), to: grabRef, count: slot.count } }];
  }
  return [];
}

// 右クリック: grab保持なら1個置き / 空手なら block_inventory.split（半分掴みはホスト計算。stale な client 数量に依存しない）
// Right click: place one while holding grab / block_inventory.split empty-handed (the host computes the half; no stale client count)
export function planBlockRightClick(index: number, slot: SlotData, grabCount: number): PlannedAction[] {
  if (grabCount > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: grabRef, to: blockRef(index), count: 1 } }];
  }
  if (slot.count === 0) return [];
  return [{ type: "block_inventory.split", payload: { from: blockRef(index) } }];
}

// ダブルクリック: 収集先（grab かクリックスロットか）はホストが自身の grab 状態で決める
// Double click: the host decides the target (grab vs clicked slot) from its own grab state
export function planBlockDoubleClick(index: number): PlannedAction[] {
  return [{ type: "block_inventory.collect", payload: { slot: blockRef(index) } }];
}
