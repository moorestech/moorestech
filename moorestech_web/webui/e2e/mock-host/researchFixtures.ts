import type { ResearchTreeData } from "../../src/bridge/contract/payloadTypes";

// FEAT-RES-1 研究ツリー: 完了済み/前提不足/研究可能の3状態を含む
// FEAT-RES-1 research tree: contains completed / pre-node-lacking / researchable states
// 3ノード目は state:researchable + 所持済みアイテムのみ消費で、研究実行→completed 遷移を e2e で検証できる
// The 3rd node is researchable and consumes only owned items so e2e can verify research→completed transition
export const researchTree = {
  nodes: [
    {
      guid: "11111111-1111-1111-1111-111111111111",
      name: "最初の研究",
      description: "説明テキスト",
      state: "completed",
      iconItemId: 2,
      position: { x: 0.0, y: 0.0 },
      prevGuids: [],
      consumeItems: [{ itemId: 1, count: 5 }],
      rewardItems: [{ itemId: 2, count: 4 }],
      unlockItemIds: [],
    },
    {
      guid: "22222222-2222-2222-2222-222222222222",
      name: "次の研究",
      description: "前提つき",
      state: "unresearchableNotEnoughPreNode",
      iconItemId: 3,
      position: { x: 300.0, y: -120.0 },
      prevGuids: ["11111111-1111-1111-1111-111111111111"],
      consumeItems: [],
      rewardItems: [],
      unlockItemIds: [3],
    },
    {
      guid: "33333333-3333-3333-3333-333333333333",
      name: "実行可能な研究",
      description: "所持アイテムで研究できる",
      state: "researchable",
      iconItemId: 100,
      position: { x: 600.0, y: 0.0 },
      prevGuids: ["11111111-1111-1111-1111-111111111111"],
      consumeItems: [{ itemId: 1, count: 5 }],
      rewardItems: [{ itemId: 100, count: 2 }],
      unlockItemIds: [],
    },
  ],
} satisfies ResearchTreeData;
