import { dispatchAction } from "@/bridge";
import { asElement } from "@/shared/pointerGesture/dragThreshold";
import { usePressGesture } from "@/shared/pointerGesture/usePressGesture";
import { resolveDropAction, type HotbarDragSource, type HotbarDropTarget } from "./hotbarDnd";

// 汎用のポインタ判定へホットバー固有のドロップ解決だけを与える
// Supplies the hotbar-specific drop resolution to the generic pointer classification
export function useHotbarDragSource(source: HotbarDragSource | null, onTap: () => void) {
  // pointerdownでテキスト選択・フォーカス移動を抑止する
  // preventDefault on pointerdown suppresses text selection over the label/icon and focus shift
  const press = usePressGesture({
    onPressStart: (event) => event.preventDefault(),
    // ドラッグ確定後は、掴む物が無ければ何も起こさない（タップへは落とさない）
    // Once a drag is settled, an absent source simply does nothing; it never falls back to a tap
    onDragEnd: (event) => {
      if (source !== null) resolveAndDispatchDrop(source, event.clientX, event.clientY);
    },
    onTap: () => onTap(),
  });

  return press.handlers;
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
