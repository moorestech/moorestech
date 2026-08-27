import type { PointerEvent } from "react";
import { asElement } from "@/shared/pointerGesture/dragThreshold";
import { usePressGesture } from "@/shared/pointerGesture/usePressGesture";
import { toCssScale } from "./viewport";
import type { PanInertia } from "./viewport";

type Options<T> = {
  panBy: (dx: number, dy: number) => void;
  inertia: PanInertia;
  // 印からノードを引く表
  // Node table keyed by the wrapper mark
  byId: Map<string, T>;
  onNodeTap?: (node: T) => void;
};

// ノード包みへ付ける印
// The mark placed on a node wrapper
export const NODE_ID_ATTRIBUTE = "data-tree-node-id";
const nodeIdAt = (target: EventTarget | null) =>
  asElement(target)?.closest(`[${NODE_ID_ATTRIBUTE}]`)?.getAttribute(NODE_ID_ATTRIBUTE) ?? null;

// ノード押下もパン、閾値未満の解放だけタップにする
// A press on a node pans too; only a release under the threshold taps
// ビューポートは押下時にポインタを捕捉するため、ノード内のnative clickは届かない。押下由来の操作は必ずonNodeTapへ配線する
// The viewport captures the pointer at press time, so a native click inside a node never fires; wire press-driven actions through onNodeTap
export function useTreePanGesture<T>({ panBy, inertia, byId, onNodeTap }: Options<T>) {
  const press = usePressGesture({
    onDragMove: (event, move) => {
      const scale = toCssScale(event.currentTarget);
      const dx = move.sinceLastX * scale;
      const dy = move.sinceLastY * scale;
      inertia.trackMove(dx, dy);
      panBy(dx, dy);
    },
    onDragEnd: () => inertia.release(),
    onTap: (target) => {
      const nodeId = nodeIdAt(target);
      const node = nodeId === null ? undefined : byId.get(nodeId);
      if (node) onNodeTap?.(node);
    },
    onAbort: (dragged) => {
      if (dragged) inertia.cancel();
    },
  });

  // 押下は受理の可否によらず滑走を止める
  // Any press stops the glide, accepted or not
  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    inertia.cancel();
    press.handlers.onPointerDown(event);
  };

  return {
    isPanning: press.dragging,
    viewportHandlers: { ...press.handlers, onPointerDown },
  };
}
