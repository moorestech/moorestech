import { readTopic, Topics } from "@/bridge";
import type { ItemMasterEntry, PlayerInventoryData, SlotData, SlotRef } from "@/bridge/contract/payloadTypes";
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
  onLeftDown: (ref: SlotRef, slot: SlotData, shiftKey: boolean) => void;
  onRightDown: (ref: SlotRef, slot: SlotData) => void;
  onDoubleClick: (ref: SlotRef) => void;
};

// 判定は shared/itemMove の純関数プランナに委譲し、ここは topic 読み出しと送信の配線だけを持つ
// Decisions live in the shared/itemMove pure planners; this file only wires topic reads to dispatch
export function createSlotActions(
  inventory: PlayerInventoryData,
  itemMaster: Map<number, ItemMasterEntry> | null,
): SlotActions {
  const onLeftDown = (ref: SlotRef, slot: SlotData, shiftKey: boolean) => {
    // block 開閉は event 時点の最新値を readTopic で読む（キー入力リスナーと同じ規約）
    // Read the block open state at event time via readTopic (same contract as the keydown listener)
    const block = readTopic(Topics.blockInventory);
    const ctx: PlayerSlotContext = {
      inventory,
      // マスタ未ロード時は maxStack 不明として planDirectMoves が空スロットのみ使う
      // With the master unloaded, maxStack is unknown and planDirectMoves falls back to empty slots
      maxStack: itemMaster?.get(slot.itemId)?.maxStack,
      blockItemSlots: block?.open ? block.itemSlots : null,
    };
    dispatchPlanned(planPlayerLeftClick(ref, slot, shiftKey, ctx));
  };

  const onRightDown = (ref: SlotRef, slot: SlotData) => {
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  };

  const onDoubleClick = (ref: SlotRef) => {
    dispatchPlanned(planPlayerDoubleClick(ref));
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
