import type { ChallengeNodeData } from "@/bridge";

// 寄せ先は進行中。無ければ寄せない
// Focus the in-progress challenge; none means no focus
export function findChallengeInitialFocusNode(nodes: ChallengeNodeData[]): ChallengeNodeData | null {
  return nodes.find((node) => node.state === "current") ?? null;
}
