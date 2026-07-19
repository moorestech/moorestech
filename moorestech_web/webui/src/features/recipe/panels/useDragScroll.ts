import { useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import { exceededThreshold, nextScrollTop } from "./dragScrollMath";

// 進行中ジェスチャの押下時スナップショット。移動量とタップ判定の基準にする
// Snapshot of a gesture at press time; the baseline for movement and tap detection
type Gesture = {
  pointerId: number;
  startX: number;
  startY: number;
  startScrollTop: number;
  target: HTMLElement | null;
  dragged: boolean;
};

type Options = {
  // ドラッグせず離した時に、押下点のDOMを渡して選択を確定させる
  // On release without dragging, hand the press-point DOM up so the caller can commit selection
  onTap: (target: HTMLElement) => void;
};

// 押下点が要素なら返す。closestを持つかで判定しinstanceofのグローバル依存を避ける
// Return the press target when it is an element; probe for closest to avoid a global instanceof dependency
function asElement(target: EventTarget | null): HTMLElement | null {
  return target && typeof (target as HTMLElement).closest === "function" ? (target as HTMLElement) : null;
}

// ScrollAreaのviewportに配線し、掴んで上下ドラッグで縦スクロールさせる
// Wire onto a ScrollArea viewport to scroll vertically by grabbing and dragging up/down
export function useDragScroll({ onTap }: Options) {
  const gesture = useRef<Gesture | null>(null);
  const [dragging, setDragging] = useState(false);

  // 主ポインタの左押下のみ受け付け、押下時点で捕捉して終了イベントを取りこぼさない
  // Accept only the primary left press and capture at press time so end events are never missed
  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!event.isPrimary || event.button !== 0) return;
    // 前例TreeView同様pointerdownで即捕捉。閾値前に窓外へ出てもup/cancelが必ず届きジェスチャが残らない
    // Capture on pointerdown like TreeView; up/cancel always arrive even if leaving before the threshold, so no gesture leaks
    event.currentTarget.setPointerCapture(event.pointerId);
    gesture.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      startScrollTop: event.currentTarget.scrollTop,
      target: asElement(event.target),
      dragged: false,
    };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;

    // 閾値超えで初めてドラッグ確定。以降はスクロールに徹する
    // Commit to a drag only past the threshold, then focus purely on scrolling
    if (!g.dragged && exceededThreshold(event.clientX - g.startX, event.clientY - g.startY)) {
      g.dragged = true;
      setDragging(true);
    }
    if (g.dragged) {
      // scrollTopはブラウザが有効範囲へ自動クランプするため手動制限は不要
      // The browser auto-clamps scrollTop to the valid range, so no manual bounds are needed
      event.currentTarget.scrollTop = nextScrollTop(g.startScrollTop, g.startY, event.clientY);
    }
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;
    // ドラッグ済みなら選択せず終了、未ドラッグなら押下点をタップとして選択
    // If it dragged, end without selecting; otherwise treat the press point as a tap selection
    if (g.dragged) setDragging(false);
    else if (g.target) onTap(g.target);
  };

  // キャンセルや捕捉喪失はドラッグの中断であり、タップとしては扱わない
  // Cancel or lost capture is an aborted drag, never treated as a tap
  const onPointerCancel = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;
    if (g.dragged) setDragging(false);
  };

  const viewportHandlers = {
    onPointerDown,
    onPointerMove,
    onPointerUp,
    onPointerCancel,
    onLostPointerCapture: onPointerCancel,
  };

  return { dragging, viewportHandlers };
}
