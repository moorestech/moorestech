import type { InventoryArea, PlayerInventoryData, SlotData, SlotRef } from "@/bridge";
import { planDirectMoves } from "./planDirectMoves";
import type { PlannedAction } from "./plannedAction";

export const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤースロット操作の判定材料。blockItemSlots はブロックUI開時のみ非null（Shift配分の宛先になる）
// Inputs for player-slot decisions; blockItemSlots is non-null only while a block UI is open (Shift target)
export type PlayerSlotContext = {
  inventory: PlayerInventoryData;
  maxStack: number | undefined;
  blockItemSlots: SlotData[] | null;
};

// 左クリック: grab保持なら全量置き / Shiftなら配分移動 / 中身ありなら全量掴み
// Left click: place all while holding grab / allocate on Shift / pick the whole stack when filled
export function planPlayerLeftClick(ref: SlotRef, slot: SlotData, shiftKey: boolean, ctx: PlayerSlotContext): PlannedAction[] {
  const grabCount = ctx.inventory.grab.count;
  if (grabCount > 0) return [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: grabCount } }];
  if (slot.count === 0) return [];
  if (shiftKey) return planShiftMove(ref, slot, ctx);
  return [{ type: "inventory.move_item", payload: { from: ref, to: GRAB, count: slot.count } }];
}

// 右クリック: grab保持なら1個置き / 空手なら inventory.split（半分掴みはホスト計算。stale な client 数量に依存しない）
// Right click: place one while holding grab / inventory.split empty-handed (the host computes the half; no stale client count)
export function planPlayerRightClick(ref: SlotRef, slot: SlotData, grabCount: number): PlannedAction[] {
  if (grabCount > 0) return [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: 1 } }];
  if (slot.count === 0) return [];
  return [{ type: "inventory.split", payload: { from: ref } }];
}

// ダブルクリック: 収集先（grab かクリックスロットか）はホストが自身の grab 状態で決める
// Double click: the host decides the target (grab vs clicked slot) from its own grab state
export function planPlayerDoubleClick(ref: SlotRef): PlannedAction[] {
  return [{ type: "inventory.collect", payload: { slot: ref } }];
}

// Shift+クリック: ブロックUIが開いていれば block へ、閉なら反対エリアへ配分する（uGUI DirectMover 準拠）
// Shift-click: allocate into the block while its UI is open, else into the opposite area (mirrors uGUI DirectMover)
function planShiftMove(from: SlotRef, slot: SlotData, ctx: PlayerSlotContext): PlannedAction[] {
  if (ctx.blockItemSlots) {
    return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, ctx.blockItemSlots).map((m) => ({
      type: "block_inventory.move_item",
      payload: { from, to: { area: "block", slot: m.slot }, count: m.count },
    }));
  }
  const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
  const targetSlots = targetArea === "main" ? ctx.inventory.mainSlots : ctx.inventory.hotbarSlots;
  return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, targetSlots).map((m) => ({
    type: "inventory.move_item",
    payload: { from, to: { area: targetArea, slot: m.slot }, count: m.count },
  }));
}
