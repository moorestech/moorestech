import type { ResearchNodeData, ResearchNodeState, SlotData } from "@/bridge/payloadTypes";

// uGUIのY上向き座標をCSS topへ反転写像（screenY = offsetY - y）
// Flip uGUI's Y-up coords to CSS top (screenY = offsetY - y)
export const CANVAS_PADDING = 200;

export type CanvasBounds = { width: number; height: number; offsetX: number; offsetY: number };

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

// 所持数をitemId別に集計
// Aggregate owned counts by itemId
export function buildOwnedCounts(slots: SlotData[]): Map<number, number> {
  const owned = new Map<number, number>();
  for (const slot of slots) {
    if (slot.itemId <= 0) continue;
    owned.set(slot.itemId, (owned.get(slot.itemId) ?? 0) + slot.count);
  }
  return owned;
}

export function hasEnoughItems(node: ResearchNodeData, owned: Map<number, number>): boolean {
  return node.consumeItems.every((c) => (owned.get(c.itemId) ?? 0) >= c.count);
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
  const itemsSufficient = hasEnoughItems(node, owned);
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
