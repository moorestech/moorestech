import type { ResearchNodeData, ResearchNodeState } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// state 5値の意味を1表へ畳む。stateが増えた日はここが欠損してコンパイルエラーになる
// Collapses the meaning of all 5 states into one table; a new state breaks compilation right here
type ResearchStateTraits = { preNodeMet: boolean; itemsSufficient: boolean };
const STATE_TRAITS: Record<ResearchNodeState, ResearchStateTraits> = {
  completed: { preNodeMet: true, itemsSufficient: true },
  researchable: { preNodeMet: true, itemsSufficient: true },
  unresearchableNotEnoughItem: { preNodeMet: true, itemsSufficient: false },
  unresearchableNotEnoughPreNode: { preNodeMet: false, itemsSufficient: true },
  unresearchableAllReasons: { preNodeMet: false, itemsSufficient: false },
};

// 特定変種を先に確保し、基準要求に残りを配る
// Reserve exact variants before allocating leftovers to base requirements
export function allocateResearchItems(node: ResearchNodeData, owned: Map<number, number> | null): number[] | null {
  if (owned === null) return null;
  const remaining = new Map(owned);
  const allocated = node.consumeItems.map(() => 0);
  const order = node.consumeItems.map((item, index) => ({ item, index }))
    .sort((a, b) => a.item.acceptedItemIds.length - b.item.acceptedItemIds.length);
  for (const { item, index } of order) {
    for (const id of item.acceptedItemIds) {
      const amount = Math.min(remaining.get(id) ?? 0, item.count - allocated[index]);
      allocated[index] += amount;
      remaining.set(id, (remaining.get(id) ?? 0) - amount);
    }
  }
  return allocated;
}

export function isConsumeItemLacking(node: ResearchNodeData, required: number, allocated: number | null): boolean {
  return allocated !== null && node.state !== "completed" && allocated < required;
}

export type ResearchButtonState = {
  completed: boolean;
  interactable: boolean;
  tooltipKey: TranslationKey;
};

// ボタン活性の正本はサーバーstateのみ。所持数は表示にしか使わない
// Server state is the sole authority for button availability; owned counts are display-only
export function deriveResearchButton(node: ResearchNodeData): ResearchButtonState {
  if (node.state === "completed") {
    return {
      completed: true,
      interactable: false,
      tooltipKey: L.ui.research.completed,
    };
  }
  const { preNodeMet, itemsSufficient } = STATE_TRAITS[node.state];
  const tooltipKey = preNodeMet
    ? itemsSufficient
      ? L.ui.research.clickToResearch
      : L.ui.research.missingItems
    : itemsSufficient
      ? L.ui.research.missingPrerequisites
      : L.ui.research.missingItemsAndPrerequisites;
  return { completed: false, interactable: preNodeMet && itemsSufficient, tooltipKey };
}

// 4状態導出
// Derives the 4-state card appearance
export type NodeCardState = { completed: boolean; ready: boolean; locked: boolean };

export function deriveNodeCardState(node: ResearchNodeData): NodeCardState {
  const completed = node.state === "completed";
  const { interactable } = deriveResearchButton(node);
  return {
    completed,
    ready: !completed && interactable,
    locked: !completed && !STATE_TRAITS[node.state].preNodeMet,
  };
}

// 状態ラベルは枠色と同じ導出結果から引く。ADR 0044: 不可の理由（不足/前提未達）は詳細ペインが担うのでカードでは3語へ畳む
// The state label reads the same derivation as the border color; ADR 0044 leaves the "unavailable" reason to the detail pane
export function deriveNodeStateLabelKey(cardState: NodeCardState): TranslationKey {
  if (cardState.completed) return L.ui.research.completed;
  return cardState.ready ? L.ui.research.stateAvailable : L.ui.research.stateUnavailable;
}

// 初期フォーカス: 研究可能優先、無ければ素材待ち最前線
// Initial focus: researchable first, else the item-lacking frontier
export function findInitialFocusNode(nodes: ResearchNodeData[]): ResearchNodeData | null {
  return nodes.find((node) => node.state === "researchable")
    ?? nodes.find((node) => node.state === "unresearchableNotEnoughItem")
    ?? null;
}
