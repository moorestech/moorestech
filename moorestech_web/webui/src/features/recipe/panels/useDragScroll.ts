import { useRef } from "react";
import { nextScrollTop } from "@/shared/pointerGesture/dragThreshold";
import { usePressGesture } from "@/shared/pointerGesture/usePressGesture";

type Options = {
  // ドラッグせず離した時に、押下点のDOMを渡して選択を確定させる
  // On release without dragging, hand the press-point DOM up so the caller can commit selection
  onTap: (target: HTMLElement) => void;
};

// ScrollAreaのviewportに配線し、掴んで上下ドラッグで縦スクロールさせる
// Wire onto a ScrollArea viewport to scroll vertically by grabbing and dragging up/down
export function useDragScroll({ onTap }: Options) {
  const startScrollTop = useRef(0);
  const press = usePressGesture({
    onPressStart: (event) => {
      startScrollTop.current = event.currentTarget.scrollTop;
    },
    onDragMove: (event, move) => {
      // scrollTopはブラウザが有効範囲へ自動クランプするため手動制限は不要
      // The browser auto-clamps scrollTop to the valid range, so no manual bounds are needed
      event.currentTarget.scrollTop = nextScrollTop(startScrollTop.current, move.fromStartY);
    },
    onTap: (target) => {
      if (target) onTap(target);
    },
  });

  return { dragging: press.dragging, viewportHandlers: press.handlers };
}
