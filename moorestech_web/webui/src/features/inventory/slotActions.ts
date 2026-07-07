import { readTopic, dispatchAction, Topics } from "@/bridge";
import type { InventoryArea, ItemMasterEntry, PlayerInventoryData, SlotData, SlotRef } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "./inventoryLogic";

const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤースロット共通のクリック操作。InventoryPanel と HotbarPanel が共用する
// Player-slot click interactions shared by InventoryPanel and HotbarPanel
export type SlotActions = {
  onLeftDown: (ref: SlotRef, slot: SlotData, shiftKey: boolean) => void;
  onRightDown: (ref: SlotRef, slot: SlotData) => void;
  onDoubleClick: (ref: SlotRef) => void;
};

// dispatchAction の true は「受理」であり topic 反映完了ではない。表示更新は event 駆動に任せる
// dispatchAction's true means accepted, not topic-updated; rendering follows topic events
export function createSlotActions(
  inventory: PlayerInventoryData,
  itemMaster: Map<number, ItemMasterEntry> | null,
): SlotActions {
  const grabHeld = inventory.grab.count > 0;

  const onLeftDown = (ref: SlotRef, slot: SlotData, shiftKey: boolean) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: inventory.grab.count });
      return;
    }
    if (slot.count === 0) return;
    if (shiftKey) {
      directMove(ref, slot);
      return;
    }
    void dispatchAction("inventory.move_item", { from: ref, to: GRAB, count: slot.count });
  };

  const onRightDown = (ref: SlotRef, slot: SlotData) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: 1 });
      return;
    }
    if (slot.count === 0) return;
    void dispatchAction("inventory.split", { from: ref });
  };

  // 収集先（grab か クリックスロットか）は host が自身の現在 grab 状態で決める。
  // Web はクリックされたスロットを送るだけ。dblclick 時点の grab 表示は古く信用できない
  // The host decides the target (grab vs clicked slot) from its own current grab state.
  // The web only sends the clicked slot; the grab view at dblclick time is stale and untrustworthy
  const onDoubleClick = (ref: SlotRef) => {
    void dispatchAction("inventory.collect", { slot: ref });
  };

  // Shift+クリック: ブロックUIが開いていれば block へ、閉なら反対エリアへ配分する（uGUI DirectMover 準拠）
  // Shift-click: allocate into the block while its UI is open, else into the opposite area (mirrors uGUI DirectMover)
  const directMove = (from: SlotRef, slot: SlotData) => {
    // マスタ未ロード時は maxStack 不明として planDirectMoves が空スロットのみ使う
    // With the master unloaded, maxStack is unknown and planDirectMoves falls back to empty slots
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack;
    // block 開閉は event 時点の最新値を readTopic で読む（キー入力リスナーと同じ規約）
    // Read the block open state at event time via readTopic (same contract as the keydown listener)
    const block = readTopic(Topics.blockInventory);
    if (block?.open) {
      for (const m of planDirectMoves(slot.count, slot.itemId, maxStack, block.itemSlots)) {
        void dispatchAction("block_inventory.move_item", { from, to: { area: "block", slot: m.slot }, count: m.count });
      }
      return;
    }
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    for (const m of planDirectMoves(slot.count, slot.itemId, maxStack, targetSlots)) {
      void dispatchAction("inventory.move_item", { from, to: { area: targetArea, slot: m.slot }, count: m.count });
    }
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
