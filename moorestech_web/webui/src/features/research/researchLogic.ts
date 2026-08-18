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

export type ResearchButtonState = {
  completed: boolean;
  interactable: boolean;
  tooltipKey: TranslationKey;
};

// uGUI RefreshNodeAvailability 準拠のボタン活性/ツールチップ導出。
// ownedKnownがfalseの間（インベントリtopic未受信）はサーバーstateへフォールバックする（D4）
// Button availability/tooltip derivation mirroring uGUI RefreshNodeAvailability.
// While ownedKnown is false (inventory topic not yet received), fall back to the server state (D4)
export function deriveResearchButton(node: ResearchNodeData, owned: Map<number, number>, ownedKnown: boolean): ResearchButtonState {
  if (node.state === "completed") {
    return {
      completed: true,
      interactable: false,
      tooltipKey: L.ui.research.completed,
    };
  }
  const preNodeMet = isPreNodeMet(node.state);
  const itemsSufficient = ownedKnown ? hasEnoughItems(node.consumeItems, owned) : node.state === "researchable";
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

// カードのdata属性用の4状態導出。充足はインベントリからのライブ再計算（ADR 0014）
// Derive the card's 4-state data attributes; sufficiency is recomputed live from the inventory (ADR 0014)
export type NodeCardState = { completed: boolean; ready: boolean; locked: boolean };

export function deriveNodeCardState(node: ResearchNodeData, owned: Map<number, number>, ownedKnown: boolean): NodeCardState {
  const completed = node.state === "completed";
  const preNodeMet = isPreNodeMet(node.state);
  const { interactable } = deriveResearchButton(node, owned, ownedKnown);
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
