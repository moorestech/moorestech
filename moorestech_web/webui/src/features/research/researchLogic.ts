import type { ResearchNodeData, ResearchNodeState } from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";
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

// 消費アイテム1件の不足強調。完了/未受信中は出さない（表示専用でボタン活性には効かない）
// Per-item shortage highlight; hidden while completed or unreceived (display only, never gates the button)
export function isConsumeItemLacking(node: ResearchNodeData, itemId: number, required: number, owned: Map<number, number> | null): boolean {
  if (owned === null) return false;
  return node.state !== "completed" && !hasEnoughItems([{ itemId, count: required }], owned);
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

// カードの状態ラベル。ADR 0044: 不可の理由（不足/前提未達）は詳細ペインが担うのでカードでは3語へ畳む
// Card state label. ADR 0044: the reason for "unavailable" lives in the detail pane, so the card collapses to 3 words
export function deriveNodeStateLabelKey(node: ResearchNodeData): TranslationKey {
  const cardState = deriveNodeCardState(node);
  if (cardState.completed) return L.ui.research.stateCompleted;
  if (cardState.ready) return L.ui.research.stateAvailable;
  return L.ui.research.stateUnavailable;
}

// 初期フォーカス: 研究可能優先、無ければ素材待ち最前線
// Initial focus: researchable first, else the item-lacking frontier
export function findInitialFocusNode(nodes: ResearchNodeData[]): ResearchNodeData | null {
  return nodes.find((node) => node.state === "researchable")
    ?? nodes.find((node) => node.state === "unresearchableNotEnoughItem")
    ?? null;
}
