import type { ResearchNodeData, ResearchNodeState } from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";
import { L, type TranslationKey } from "@/shared/i18n";

// 前提研究が済んでいるか（uGUIは前提未達を専用stateで表すため状態から逆算）
// Whether prerequisites are met (uGUI encodes unmet prereqs as dedicated states, so infer from state)
function isPreNodeMet(state: ResearchNodeState): boolean {
  return state === "researchable" || state === "unresearchableNotEnoughItem";
}

// 消費アイテム1件が所持数を満たすか
// Whether one consume item is satisfied by owned count
export function isItemSufficient(itemId: number, required: number, owned: Map<number, number>): boolean {
  return hasEnoughItems([{ itemId, count: required }], owned);
}

// 完了/未受信中は不足強調しない
// Suppressed while completed or owned counts unknown
export function isConsumeItemLacking(node: ResearchNodeData, itemId: number, required: number, owned: Map<number, number> | null): boolean {
  if (owned === null) return false;
  return node.state !== "completed" && !isItemSufficient(itemId, required, owned);
}

export type ResearchButtonState = {
  completed: boolean;
  interactable: boolean;
  tooltipKey: TranslationKey;
};

// uGUI準拠のボタン活性導出
// 所持数未受信(null)の間はサーバーstateへ(D4)
// Button availability derivation mirroring uGUI
// Falls back to server state while owned counts are unreceived (null) (D4)
export function deriveResearchButton(node: ResearchNodeData, owned: Map<number, number> | null): ResearchButtonState {
  if (node.state === "completed") {
    return {
      completed: true,
      interactable: false,
      tooltipKey: L.ui.research.completed,
    };
  }
  const preNodeMet = isPreNodeMet(node.state);
  // 未受信中の充足はサーバーstateから読む。前提未達でアイテムは足りている状態も充足側に数える
  // While unreceived, sufficiency is read off the server state; the prereq-only shortfall counts as sufficient too
  const itemsSufficient = owned !== null
    ? hasEnoughItems(node.consumeItems, owned)
    : node.state === "researchable" || node.state === "unresearchableNotEnoughPreNode";
  const interactable = preNodeMet && itemsSufficient;
  const tooltipKey = preNodeMet
    ? itemsSufficient
      ? L.ui.research.clickToResearch
      : L.ui.research.missingItems
    : itemsSufficient
      ? L.ui.research.missingPrerequisites
      : L.ui.research.missingItemsAndPrerequisites;
  return { completed: false, interactable, tooltipKey };
}

// 4状態導出。充足はライブ再計算
// Derives the 4-state; sufficiency recomputed live
export type NodeCardState = { completed: boolean; ready: boolean; locked: boolean };

export function deriveNodeCardState(node: ResearchNodeData, owned: Map<number, number> | null): NodeCardState {
  const completed = node.state === "completed";
  const preNodeMet = isPreNodeMet(node.state);
  const { interactable } = deriveResearchButton(node, owned);
  return {
    completed,
    ready: !completed && interactable,
    locked: !completed && !preNodeMet,
  };
}

// 初期フォーカス: 研究可能優先、無ければ素材待ち最前線
// Initial focus: researchable first, else the item-lacking frontier
export function findInitialFocusNode(nodes: ResearchNodeData[]): ResearchNodeData | null {
  return nodes.find((node) => node.state === "researchable")
    ?? nodes.find((node) => node.state === "unresearchableNotEnoughItem")
    ?? null;
}
