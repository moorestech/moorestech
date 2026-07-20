import type { ResearchNodeData, ResearchNodeState } from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";

// uGUIのY上向き座標をCSS topへ反転写像（screenY = offsetY - y）
// Flip uGUI's Y-up coords to CSS top (screenY = offsetY - y)
export const CANVAS_PADDING = 200;

export type CanvasBounds = { width: number; height: number; offsetX: number; offsetY: number };

export const MIN_VIEW_SCALE = 0.4;
export const MAX_VIEW_SCALE = 2.5;
export const WHEEL_ZOOM_SENSITIVITY = 0.0015;

export type ViewportTransform = { x: number; y: number; scale: number };
export type Point = { x: number; y: number };

// カーソル下のワールド座標を保ったままホイールズームする
// Wheel-zoom while keeping the cursor's world point fixed
export function zoomViewportAt(
  viewport: ViewportTransform,
  cursor: Point,
  deltaY: number,
): ViewportTransform {
  const scale = Math.min(
    MAX_VIEW_SCALE,
    Math.max(MIN_VIEW_SCALE, viewport.scale * Math.exp(-deltaY * WHEEL_ZOOM_SENSITIVITY)),
  );
  const worldX = (cursor.x - viewport.x) / viewport.scale;
  const worldY = (cursor.y - viewport.y) / viewport.scale;
  return {
    x: cursor.x - worldX * scale,
    y: cursor.y - worldY * scale,
    scale,
  };
}

export function computeCanvasBounds(nodes: ResearchNodeData[]): CanvasBounds {
  if (nodes.length === 0) {
    return { width: CANVAS_PADDING * 2, height: CANVAS_PADDING * 2, offsetX: CANVAS_PADDING, offsetY: CANVAS_PADDING };
  }
  const xs = nodes.map((n) => n.position.x);
  const ys = nodes.map((n) => n.position.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  return {
    width: maxX - minX + CANVAS_PADDING * 2,
    height: maxY - minY + CANVAS_PADDING * 2,
    offsetX: CANVAS_PADDING - minX,
    offsetY: maxY + CANVAS_PADDING,
  };
}

// uGUI同等の接続線計算
// Connection line calculation matching uGUI
export type Line = { x: number; y: number; length: number; angleDeg: number };

export function lineBetween(from: { x: number; y: number }, to: { x: number; y: number }): Line {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  return { x: from.x, y: from.y, length: Math.hypot(dx, dy), angleDeg: (Math.atan2(dy, dx) * 180) / Math.PI };
}

// 前提研究が済んでいるか（uGUIは前提未達を専用stateで表すため状態から逆算）
// Whether prerequisites are met (uGUI encodes unmet prereqs as dedicated states, so infer from state)
export function isPreNodeMet(state: ResearchNodeState): boolean {
  return state === "researchable" || state === "unresearchableNotEnoughItem";
}

// 消費アイテム1件が所持数を満たすか（completedは常に非ハイライト）
// Whether one consume item is satisfied by owned count (completed is never highlighted)
export function isItemSufficient(
  node: ResearchNodeData,
  itemId: number,
  required: number,
  owned: Map<number, number>,
): boolean {
  return node.state !== "completed" && (owned.get(itemId) ?? 0) >= required;
}

export type ResearchButtonState = { completed: boolean; interactable: boolean; tooltip: string };

// uGUI RefreshNodeAvailability 準拠のボタン活性/ツールチップ導出
// Button availability/tooltip derivation mirroring uGUI RefreshNodeAvailability
export function deriveResearchButton(node: ResearchNodeData, owned: Map<number, number>): ResearchButtonState {
  if (node.state === "completed") return { completed: true, interactable: false, tooltip: "研究済み" };
  const preNodeMet = isPreNodeMet(node.state);
  const itemsSufficient = hasEnoughItems(node.consumeItems, owned);
  const interactable = preNodeMet && itemsSufficient;
  const tooltip = preNodeMet
    ? itemsSufficient
      ? "クリックして研究"
      : "研究アイテムが足りません。"
    : itemsSufficient
      ? "前提研究が完了していません。"
      : "研究アイテムが足りません。\n前提研究が完了していません。";
  return { completed: false, interactable, tooltip };
}

// カードのdata属性用の状態導出（lockedは前提未達）
// Derive card data-attribute state (locked = prerequisites unmet)
export type NodeCardState = { completed: boolean; researchable: boolean; locked: boolean };

export function deriveNodeCardState(state: ResearchNodeState): NodeCardState {
  return {
    completed: state === "completed",
    researchable: state === "researchable",
    locked: state !== "completed" && !isPreNodeMet(state),
  };
}
