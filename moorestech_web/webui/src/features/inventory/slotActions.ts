import { readItemMaster, readTopic, Topics } from "@/bridge";
import type { PlayerInventoryData, SlotData, SlotRef } from "@/bridge";
import {
  dispatchPlanned,
  planPlayerDoubleClick,
  planPlayerLeftClick,
  planPlayerRightClick,
  type PlayerSlotContext,
} from "@/shared/itemMove";

// プレイヤースロット共通のクリック操作。InventoryPanel と HotbarPanel が共用する
// Player-slot click interactions shared by InventoryPanel and HotbarPanel
export type SlotActions = {
  onLeftDown: (ref: SlotRef, shiftKey: boolean) => void;
  onRightDown: (ref: SlotRef) => void;
  onRightEnter: (ref: SlotRef) => void;
  onDoubleClick: (ref: SlotRef) => void;
};

// 判定は shared/itemMove の純関数プランナに委譲し、ここは topic 読み出しと送信の配線だけを持つ
// Decisions live in the shared/itemMove pure planners; this file only wires topic reads to dispatch
export const slotActions: SlotActions = {
  onLeftDown: (ref, shiftKey) => {
    // 全プラン入力をイベント時に読み、レンダー時 snapshot の混在を防ぐ
    // Read every planner input at event time to avoid mixing render-time snapshots
    const inventory = readTopic(Topics.inventory);
    if (!inventory) return;
    const block = readTopic(Topics.blockInventory);
    const slot = resolveSlot(inventory, ref);
    const ctx: PlayerSlotContext = {
      inventory,
      maxStack: readItemMaster()?.get(slot.itemId)?.maxStack,
      blockItemSlots: block?.open ? block.itemSlots : null,
    };
    dispatchPlanned(planPlayerLeftClick(ref, slot, shiftKey, ctx));
  },

  onRightDown: (ref) => {
    const inventory = readTopic(Topics.inventory);
    if (!inventory) return;
    const slot = resolveSlot(inventory, ref);
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  },

  onRightEnter: (ref) => {
    // 空手の連続半分取りを防ぐ
    // Never chain split-pickups while empty-handed; place one only while holding a grab stack
    const inventory = readTopic(Topics.inventory);
    if (!inventory || inventory.grab.count <= 0) return;
    const slot = resolveSlot(inventory, ref);
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  },

  onDoubleClick: (ref) => {
    dispatchPlanned(planPlayerDoubleClick(ref));
  },
};

function resolveSlot(inventory: PlayerInventoryData, ref: SlotRef): SlotData {
  return ref.area === "main" ? inventory.mainSlots[ref.slot] : inventory.hotbarSlots[ref.slot];
}
