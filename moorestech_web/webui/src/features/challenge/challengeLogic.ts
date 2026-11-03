import type { ChallengeNodeData } from "@/bridge";

// 初回表示で寄せる先は進行中のチャレンジ。全達成・未開始のカテゴリは寄せない（研究ツリーの findInitialFocusNode と同形）
// The initial focus is the in-progress challenge; categories with none get no focus (same shape as research's findInitialFocusNode)
export function findChallengeInitialFocusNode(nodes: ChallengeNodeData[]): ChallengeNodeData | null {
  return nodes.find((node) => node.state === "current") ?? null;
}
