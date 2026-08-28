import { useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import { asElement, exceededThreshold } from "./dragThreshold";

// 進行中の押下。パン量の基準(last)とタップ判定の基準(start)を併せ持つ
// The live press, holding both the movement baseline (last) and the tap baseline (start)
type Press = {
  pointerId: number;
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
  dragged: boolean;
  target: HTMLElement | null;
};

// ドラッグ中の移動量。押下点からの全量と前回からの増分
// A drag move: the whole delta from the press point and the increment since the last one
export type PressMove = {
  fromStartX: number;
  fromStartY: number;
  sinceLastX: number;
  sinceLastY: number;
};

type Options = {
  // 押下を受理した直後。捕捉前に呼ぶのでpreventDefaultや押下時の状態取りに使う
  // Right after a press is accepted, before capture; use it for preventDefault or press-time snapshots
  onPressStart?: (event: ReactPointerEvent<HTMLDivElement>) => void;
  onDragMove?: (event: ReactPointerEvent<HTMLDivElement>, move: PressMove) => void;
  onDragEnd?: (event: ReactPointerEvent<HTMLDivElement>) => void;
  // 閾値未満の解放。押下点のDOMを渡す
  // A release under the threshold, handing back the press-point DOM
  onTap?: (target: HTMLElement | null) => void;
  onAbort?: (dragged: boolean) => void;
};

// 押下をタップとドラッグへ分ける共通の状態機械。ツリーのパン・縦スクロール・DnDはすべてここを入口にする
// The shared press state machine splitting tap from drag; tree panning, drag scrolling and DnD all enter here
export function usePressGesture({ onPressStart, onDragMove, onDragEnd, onTap, onAbort }: Options) {
  const [dragging, setDragging] = useState(false);
  const press = useRef<Press | null>(null);

  // 主ポインタの左押下のみ受け付け、押下時点で捕捉して終了イベントを取りこぼさない
  // Accept only the primary left press and capture at press time so end events are never missed
  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!event.isPrimary || event.button !== 0) return;
    // 進行中の押下は第2ポインタに乗っ取らせない。乗っ取るとpointerIdがずれ解放が届かず掴み状態が固着する
    // Never let a second pointer hijack a live press; the pointerId would drift so no release arrives and the grab sticks
    if (press.current) return;
    onPressStart?.(event);
    event.currentTarget.setPointerCapture(event.pointerId);
    press.current = {
      pointerId: event.pointerId,
      startX: event.clientX, startY: event.clientY,
      lastX: event.clientX, lastY: event.clientY,
      dragged: false,
      target: asElement(event.target),
    };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const live = press.current;
    if (!live || live.pointerId !== event.pointerId) return;
    // 閾値超えで初めてドラッグ確定。超えた回では押下点からの全量を送り、内容を指へぴったり追従させる
    // Commit to a drag only past the threshold; that first move sends the whole delta from the press point so content tracks the pointer exactly
    if (!live.dragged) {
      if (!exceededThreshold(event.clientX - live.startX, event.clientY - live.startY)) return;
      live.dragged = true;
      setDragging(true);
    }
    onDragMove?.(event, {
      fromStartX: event.clientX - live.startX, fromStartY: event.clientY - live.startY,
      sinceLastX: event.clientX - live.lastX, sinceLastY: event.clientY - live.lastY,
    });
    live.lastX = event.clientX;
    live.lastY = event.clientY;
  };

  const endPress = (event: ReactPointerEvent<HTMLDivElement>) => {
    const live = press.current;
    if (!live || live.pointerId !== event.pointerId) return null;
    press.current = null;
    if (live.dragged) setDragging(false);
    return live;
  };

  // ドラッグ済みの解放はドロップ、未ドラッグはタップ
  // A dragged release drops; an undragged one taps
  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    // 主ボタン以外の解放は進行中の押下を終わらせない。終わらせるとタップが誤発火する
    // A non-primary button release never ends the live press; ending it would misfire a tap
    if (event.button !== 0) return;
    const live = endPress(event);
    if (!live) return;
    if (live.dragged) onDragEnd?.(event);
    else onTap?.(live.target);
  };

  // キャンセルや捕捉喪失は中断であり、タップにもドロップにもしない
  // Cancel or lost capture aborts the gesture; it becomes neither a tap nor a drop
  const onPointerAbort = (event: ReactPointerEvent<HTMLDivElement>) => {
    const live = endPress(event);
    if (live) onAbort?.(live.dragged);
  };

  return {
    dragging,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp,
      onPointerCancel: onPointerAbort,
      onLostPointerCapture: onPointerAbort,
    },
  };
}
