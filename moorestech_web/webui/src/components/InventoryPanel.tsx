import { useEffect, useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import { dispatchAction } from "../bridge/actions";
import type { InventoryArea, PlayerInventoryData, SlotData, SlotRef } from "../types/inventory";
import ItemSlot from "./ItemSlot";

const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤーインベントリ（メイン4行+ホットバー1行+grab）の表示と操作
// Player inventory view & interactions: 4 main rows, 1 hotbar row, and the grab stack
export default function InventoryPanel() {
  const inventory = useTopic<PlayerInventoryData>("local_player.inventory");
  const itemMaster = useItemMaster();
  const [mousePos, setMousePos] = useState({ x: 0, y: 0 });

  useEffect(() => {
    const onMove = (e: globalThis.MouseEvent) => setMousePos({ x: e.clientX, y: e.clientY });
    window.addEventListener("mousemove", onMove);
    return () => window.removeEventListener("mousemove", onMove);
  }, []);

  if (!inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
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

  // 収集はメイン+ホットバー+開いているサブインベントリ全域が対象（uGUI のダブルクリックと同じ）
  // Collect sweeps main, hotbar, and any open sub inventory, matching uGUI double-click
  const onDoubleClick = (ref: SlotRef, slot: SlotData) => {
    if (!grabHeld && slot.count === 0) return;
    void dispatchAction("inventory.collect", { target: grabHeld ? GRAB : ref });
  };

  // Shift+クリック: 反対エリアの同種スタック→空スロットの順で移動先を探す
  // Shift-click: prefer a same-item stack in the opposite area, then an empty slot
  const directMove = (from: SlotRef, slot: SlotData) => {
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack ?? Infinity;

    const stackable = targetSlots.findIndex((s) => s.itemId === slot.itemId && s.count < maxStack);
    const empty = targetSlots.findIndex((s) => s.count === 0);
    const target = stackable >= 0 ? stackable : empty;
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
        onDoubleClick={() => onDoubleClick(ref, slot)}
      />
    );
  };

  return (
    <div className="space-y-3">
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
      <div className="grid grid-cols-9 gap-1 w-fit pt-1 border-t border-gray-600">
        {inventory.hotbarSlots.map((s, i) => renderSlot("hotbar", i, s))}
      </div>
      {grabHeld ? (
        <div
          className="pointer-events-none fixed z-40 w-12 h-12"
          style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}
        >
          <ItemSlot itemId={inventory.grab.itemId} count={inventory.grab.count} />
        </div>
      ) : null}
    </div>
  );
}
