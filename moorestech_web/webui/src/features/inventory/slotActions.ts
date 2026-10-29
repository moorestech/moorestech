import { dispatchAction, readItemMaster, readTopic, Topics } from "@/bridge";
import type { PlayerInventoryData, SlotData, SlotRef } from "@/bridge";
import {
  dispatchPlanned,
  planPlayerDoubleClick,
  planPlayerLeftClick,
  planPlayerRightClick,
  type PlayerSlotContext,
} from "@/shared/itemMove";
import { useNotificationStore } from "@/features/notification";
import { resolveBlockSlotRestriction } from "./blockSlotRestriction";
import { SplitDragSession } from "./splitDrag";

const splitDrag = new SplitDragSession((slots) => void dispatchAction("inventory.split_drag", { slots }));
if (typeof window !== "undefined") window.addEventListener("mouseup", () => splitDrag.end());

// 3パネル共通のスロット操作
// Slot actions shared by all three panels
export type SlotActions = {
  onLeftDown: (ref: SlotRef, shiftKey: boolean) => void;
  onRightDown: (ref: SlotRef) => void;
  onRightEnter: (ref: SlotRef) => void;
  onLeftEnter: (ref: SlotRef) => void;
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
    const slot = resolveSlot(inventory, ref);
    if (!slot) return;
    const block = readTopic(Topics.blockInventory);
    const ctx: PlayerSlotContext = {
      inventory,
      maxStack: readItemMaster()?.get(slot.itemId)?.maxStack,
      blockItemSlots: block?.open ? block.itemSlots : null,
      blockSlotRestriction: resolveBlockSlotRestriction(block, slot.itemId),
    };
    const plan = planPlayerLeftClick(ref, slot, shiftKey, ctx);
    if (plan.kind === "beginSplitDrag") { splitDrag.begin(ref); return; }
    if (plan.kind === "noAcceptingBlockSlot") { notifyNoAcceptingBlockSlot(slot.itemId, block?.open ? block.identifier : null); return; }
    dispatchPlanned(plan.actions);
  },

  onRightDown: (ref) => {
    const inventory = readTopic(Topics.inventory);
    if (!inventory) return;
    const slot = resolveSlot(inventory, ref);
    if (!slot) return;
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  },

  onRightEnter: (ref) => {
    // 空手の連続半分取りを防ぐ
    // Never chain split-pickups while empty-handed; place one only while holding a grab stack
    const inventory = readTopic(Topics.inventory);
    if (!inventory || inventory.grab.count <= 0) return;
    const slot = resolveSlot(inventory, ref);
    if (!slot) return;
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  },

  onLeftEnter: (ref) => {
    const inventory = readTopic(Topics.inventory);
    if (!inventory || !resolveSlot(inventory, ref)) return;
    splitDrag.enter(ref);
  },

  onDoubleClick: (ref) => {
    const inventory = readTopic(Topics.inventory);
    if (!inventory || !resolveSlot(inventory, ref)) return;
    dispatchPlanned(planPlayerDoubleClick(ref));
  },
};

// 受け付ける枠が無いShiftは送らずに拒否する。サーバーが同じ拒否で出す通知と同じ文言をユーザーへ出し、開発者へは理由をログで残す
// A Shift with no accepting slot is refused without sending: users get the same notice the server raises for this refusal, developers get the reason in the log
function notifyNoAcceptingBlockSlot(itemId: number, blockIdentifier: string | null) {
  console.warn(`[inventory.shift_move] not sent: no slot of block ${blockIdentifier ?? "(closed)"} accepts item ${itemId} (unaccepted item or bindings not yet received)`);
  useNotificationStore.getState().addNotification({ category: "operationDenied", messageId: "denied.inventoryMoveSlotRejected", messageParams: [], itemId: null });
}

// 枠数が縮んだ直後の stale クリックは対象が存在しないため undefined を返し、呼び出し側が無操作で降りる
// A stale click right after the slot count shrinks has no target, so return undefined and let callers bail out
function resolveSlot(inventory: PlayerInventoryData, ref: SlotRef): SlotData | undefined {
  if (ref.area === "grab") return inventory.grab;
  if (ref.area === "equipment") return inventory.equipment[ref.slot];
  return inventory.mainSlots[ref.slot];
}
