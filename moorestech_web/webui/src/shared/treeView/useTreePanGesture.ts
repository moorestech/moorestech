import { useRef, useState } from "react";
import type { PointerEvent } from "react";
import { asElement, exceededThreshold } from "@/shared/pointerGesture/dragThreshold";
import { toCssScale } from "./viewport";
import type { PanInertia } from "./viewport";

// 進行中ジェスチャの押下時スナップショット。パン量の基準(last)とタップ判定の基準(start)を併せ持つ
// Snapshot of a gesture at press time, holding both the pan baseline (last) and the tap baseline (start)
type PanGesture = {
  pointerId: number;
  lastX: number;
  lastY: number;
  startX: number;
  startY: number;
  dragged: boolean;
  nodeId: string | null;
};

type Options<T> = {
  panBy: (dx: number, dy: number) => void;
  inertia: PanInertia;
  // 押下点の印から引くノード表。タップの通知対象をここで解決する
  // Node table keyed by the wrapper mark; resolves which node a tap notifies
  byId: Map<string, T>;
  onNodeTap?: (node: T) => void;
};

// ノード包みへ付ける印。押下点からこの祖先を辿ってタップ対象のノードを引く
// The wrapper mark; a press point walks up to this ancestor to resolve the tapped node
export const NODE_ID_ATTRIBUTE = "data-tree-node-id";
const nodeIdAt = (target: EventTarget | null) =>
  asElement(target)?.closest(`[${NODE_ID_ATTRIBUTE}]`)?.getAttribute(NODE_ID_ATTRIBUTE) ?? null;

// ツリーの掴み操作。ノード上の押下もパンとして受け、ドラッグに至らなかった解放だけを選択にする
// The tree grab gesture: a press on a node pans too, and only a release that never became a drag selects
export function useTreePanGesture<T>({ panBy, inertia, byId, onNodeTap }: Options<T>) {
  const [isPanning, setIsPanning] = useState(false);
  const gesture = useRef<PanGesture | null>(null);

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    inertia.cancel();
    if (!event.isPrimary || event.button !== 0) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    gesture.current = {
      pointerId: event.pointerId,
      lastX: event.clientX, lastY: event.clientY,
      startX: event.clientX, startY: event.clientY,
      dragged: false,
      nodeId: nodeIdAt(event.target),
    };
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const pan = gesture.current;
    if (!pan || pan.pointerId !== event.pointerId) return;
    // 閾値超えで初めてドラッグ確定。超えた回では押下点からの全量を送り、内容を指へぴったり追従させる
    // Commit to a drag only past the threshold; that first move pans the whole delta from the press point so content tracks the pointer exactly
    if (!pan.dragged) {
      if (!exceededThreshold(event.clientX - pan.startX, event.clientY - pan.startY)) return;
      pan.dragged = true;
      setIsPanning(true);
    }
    const scale = toCssScale(event.currentTarget);
    const dx = (event.clientX - pan.lastX) * scale;
    const dy = (event.clientY - pan.lastY) * scale;
    inertia.trackMove(dx, dy);
    panBy(dx, dy);
    pan.lastX = event.clientX;
    pan.lastY = event.clientY;
  };

  const endGesture = (event: PointerEvent<HTMLDivElement>) => {
    const pan = gesture.current;
    if (!pan || pan.pointerId !== event.pointerId) return null;
    gesture.current = null;
    if (pan.dragged) setIsPanning(false);
    return pan;
  };

  // ドラッグ済みのpointerupのみ滑走、未ドラッグはタップとして選択、他は中断
  // Only a dragged pointerup flings; an undragged one taps to select, and the rest abort
  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const pan = endGesture(event);
    if (!pan) return;
    if (pan.dragged) {
      inertia.release();
      return;
    }
    const node = pan.nodeId === null ? undefined : byId.get(pan.nodeId);
    if (node) onNodeTap?.(node);
  };

  const onPointerAbort = (event: PointerEvent<HTMLDivElement>) => {
    if (endGesture(event)?.dragged) inertia.cancel();
  };

  return {
    isPanning,
    viewportHandlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp,
      onPointerCancel: onPointerAbort,
      onLostPointerCapture: onPointerAbort,
    },
  };
}
