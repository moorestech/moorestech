import { dispatchAction } from "@/bridge";
import { asElement } from "@/shared/pointerGesture/dragThreshold";
import { useDragSource } from "@/shared/pointerGesture/useDragSource";
import { resolveDropAction, type HotbarDragSource, type HotbarDropTarget } from "./hotbarDnd";

// 汎用のポインタ判定へホットバー固有のドロップ解決だけを与える
// Supplies the hotbar-specific drop resolution to the generic pointer classification
export function useHotbarDragSource(source: HotbarDragSource | null, onTap: () => void) {
  return useDragSource(source, onTap, resolveAndDispatchDrop);
}

// 解放点のDOMをresolveDropActionへ
// Resolve the real DOM under the release point into a slot/outside endpoint, then hand off to the pure resolveDropAction
function resolveAndDispatchDrop(source: HotbarDragSource, clientX: number, clientY: number) {
  const element = asElement(document.elementFromPoint(clientX, clientY));
  const slotElement = element?.closest<HTMLElement>("[data-hotbar-slot-index]") ?? null;

  // 枠間ギャップで離すと無操作
  // Releasing in the gap between slots (still inside the HUD) is a no-op, not a clear
  if (!slotElement && element?.closest("[data-hotbar-row]")) return;

  const target: HotbarDropTarget = slotElement
    ? { kind: "hotbarSlot", index: Number(slotElement.dataset.hotbarSlotIndex) }
    : { kind: "outside" };

  const action = resolveDropAction(source, target);
  if (action) void dispatchAction(action.type, action.payload);
}
