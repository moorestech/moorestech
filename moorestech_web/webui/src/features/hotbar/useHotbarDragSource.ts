import { useRef } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import { dispatchAction } from "@/bridge";
import { asElement, exceededThreshold } from "@/shared/pointerGesture/dragThreshold";
import { resolveDropAction, type DragEndpoint } from "./hotbarDnd";

type Gesture = { pointerId: number; startX: number; startY: number; dragging: boolean };

// 両ドラッグ元共通のポインタ制御
// pointerdownでテキスト選択等を抑止
// Shared pointer control for both drag sources (build-menu entry / hotbar slot).
// preventDefault on pointerdown suppresses text selection over the label/icon and focus shift
export function useHotbarDragSource(source: DragEndpoint | null, onTap: () => void) {
  const gesture = useRef<Gesture | null>(null);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!event.isPrimary || event.button !== 0) return;
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    gesture.current = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, dragging: false };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId || source === null) return;
    if (!g.dragging && exceededThreshold(event.clientX - g.startX, event.clientY - g.startY)) g.dragging = true;
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;
    if (g.dragging && source !== null) resolveAndDispatchDrop(source, event.clientX, event.clientY);
    else onTap();
  };

  // 中断はタップ・ドロップ扱いしない
  // Cancel or lost capture aborts the gesture; treat it as neither a tap nor a drop
  const onPointerCancel = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;
  };

  return { onPointerDown, onPointerMove, onPointerUp, onPointerCancel, onLostPointerCapture: onPointerCancel };
}

// 解放点のDOMをresolveDropActionへ
// Resolve the real DOM under the release point into a slot/outside endpoint, then hand off to the pure resolveDropAction
function resolveAndDispatchDrop(source: DragEndpoint, clientX: number, clientY: number) {
  const element = asElement(document.elementFromPoint(clientX, clientY));
  const slotElement = element?.closest<HTMLElement>("[data-hotbar-slot-index]") ?? null;

  // 枠間ギャップで離すと無操作
  // Releasing in the gap between slots (still inside the HUD) is a no-op, not a clear
  if (!slotElement && element?.closest("[data-hotbar-row]")) return;

  const target: DragEndpoint = slotElement
    ? { kind: "hotbarSlot", index: Number(slotElement.dataset.hotbarSlotIndex) }
    : { kind: "outside" };

  const action = resolveDropAction(source, target);
  if (action) void dispatchAction(action.type, action.payload);
}
