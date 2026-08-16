import { useRef } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import { exceededThreshold } from "./dragThreshold";

type Gesture = { pointerId: number; startX: number; startY: number; dragging: boolean };

// 閾値未満の押下解放はタップ、超えたらドロップ。ドラッグ元を持たない要素でも判定だけは同じに走る
// A release under the threshold is a tap and beyond it is a drop; the classification runs identically even for elements holding no source
export function useDragSource<TSource>(
  source: TSource | null,
  onTap: () => void,
  onDrop: (source: TSource, clientX: number, clientY: number) => void,
) {
  const gesture = useRef<Gesture | null>(null);

  // pointerdownでテキスト選択・フォーカス移動を抑止する
  // preventDefault on pointerdown suppresses text selection over the label/icon and focus shift
  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!event.isPrimary || event.button !== 0) return;
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    gesture.current = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, dragging: false };
  };

  // ドラッグ判定はsourceの有無に依らない。空枠の大きな引きずりをタップへ落とさないため
  // The drag classification never depends on having a source, so a long drag on an empty slot is not mistaken for a tap
  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    if (!g.dragging && exceededThreshold(event.clientX - g.startX, event.clientY - g.startY)) g.dragging = true;
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const g = gesture.current;
    if (!g || g.pointerId !== event.pointerId) return;
    gesture.current = null;

    if (!g.dragging) {
      onTap();
      return;
    }

    // ドラッグ確定後は、掴む物が無ければ何も起こさない（タップへは落とさない）
    // Once a drag is settled, an absent source simply does nothing; it never falls back to a tap
    if (source !== null) onDrop(source, event.clientX, event.clientY);
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
