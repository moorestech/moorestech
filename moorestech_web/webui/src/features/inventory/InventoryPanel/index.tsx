import { useTopic, dispatchAction, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { ItemSlot } from "@/shared/ui";
import type { InventoryArea, SlotData, SlotRef } from "@/bridge/payloadTypes";
import { resolveDirectMoveTarget } from "../inventoryLogic";
import GrabOverlay from "./GrabOverlay";

const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤーインベントリ（メイン4行+ホットバー1行+grab）の表示と操作
// Player inventory view & interactions: 4 main rows, 1 hotbar row, and the grab stack
// uGUI 準拠で inv 領域（メイン+Sort）と hotbar 領域（下段中央）の2要素を Fragment で返す
// Returns two grid children via Fragment, matching uGUI: inv area (main+Sort) and bottom-center hotbar area
export default function InventoryPanel() {
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  if (!inventory) {
    return <div className="text-sm text-gray-400 [grid-area:inv]">connecting...</div>;
  }

  const grabHeld = inventory.grab.count > 0;

  // dispatchAction の true は「受理」であり topic 反映完了ではない。表示更新は event 駆動に任せる
  // dispatchAction's true means accepted, not topic-updated; rendering follows topic events
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

  // Shift+クリック: 反対エリアの同種スタック→空スロットの順で移動先を探す
  // Shift-click: prefer a same-item stack in the opposite area, then an empty slot
  const directMove = (from: SlotRef, slot: SlotData) => {
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    // マスタ未ロード時は maxStack 不明のため同種スタック探索をスキップし、空スロットのみ探す
    // Skip the same-item stack search while the item master is unloaded; fall back to empty slots
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack;
    const target = resolveDirectMoveTarget(targetSlots, slot.itemId, maxStack);
    if (target < 0) return;
    void dispatchAction("inventory.move_item", { from, to: { area: targetArea, slot: target }, count: slot.count });
  };

  const renderSlot = (area: InventoryArea, index: number, slot: SlotData) => {
    const ref: SlotRef = { area, slot: index };
    return (
      <ItemSlot
        key={`${area}-${index}`}
        itemId={slot.itemId}
        count={slot.count}
        name={itemMaster?.get(slot.itemId)?.name}
        onLeftDown={(shiftKey) => onLeftDown(ref, slot, shiftKey)}
        onRightDown={() => onRightDown(ref, slot)}
        onDoubleClick={() => onDoubleClick(ref)}
      />
    );
  };

  return (
    <>
      <div className="space-y-3 [grid-area:inv]">
        <div className="flex items-center gap-3">
          <h2 className="text-lg font-semibold">Inventory</h2>
          <button
            onClick={() => void dispatchAction("inventory.sort", {})}
            className="bg-gray-700 hover:bg-gray-600 text-sm rounded px-3 py-1"
          >
            Sort
          </button>
        </div>
        <div className="grid grid-cols-9 gap-1 w-fit">
          {inventory.mainSlots.map((s, i) => renderSlot("main", i, s))}
        </div>
      </div>
      {/* ホットバーは uGUI と同様に画面下段の中央へ独立配置 */}
      {/* The hotbar sits independently at the bottom center, matching uGUI */}
      <div className="[grid-area:hotbar] flex justify-center">
        <div className="grid grid-cols-9 gap-1 w-fit rounded border border-gray-500 bg-gray-800/60 p-1">
          {inventory.hotbarSlots.map((s, i) => renderSlot("hotbar", i, s))}
        </div>
      </div>
      <GrabOverlay grab={inventory.grab} />
    </>
  );
}
