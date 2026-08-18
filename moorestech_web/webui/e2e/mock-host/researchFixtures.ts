import type { ResearchTreeData } from "../../src/bridge/contract/payloadTypes";

// 研究可能ノードのGUID(共有SSOT)
// GUID of the researchable node (shared SSOT)
export const researchableNodeGuid = "33333333-3333-4333-8333-333333333333";

// 前提充足だがアイテム不足のノードのGUID(共有SSOT)
// GUID of the node with prerequisites met but items lacking (shared SSOT)
export const itemLackingNodeGuid = "77777777-7777-4777-8777-777777777777";

// FEAT-RES-1 研究ツリー: 完了済み/前提不足/研究可能の3状態を含む
// FEAT-RES-1 research tree: contains completed / pre-node-lacking / researchable states
// 3ノード目は state:researchable + 所持済みアイテムのみ消費で、研究実行→completed 遷移を e2e で検証できる
// The 3rd node is researchable and consumes only owned items so e2e can verify research→completed transition
export const researchTree = {
  nodes: [
    {
      guid: "11111111-1111-4111-8111-111111111111",
      state: "completed",
      iconItemId: 2,
      position: { x: 0.0, y: 0.0 },
      prevGuids: [],
      consumeItems: [{ itemId: 1, count: 5 }],
      rewardItems: [{ itemId: 2, count: 4 }],
      unlockItemIds: [],
      unlockBlocks: [],
      unlockMachineRecipeOutputItemIds: [],
      unlockConnectToolGuids: [],
      unlockTrainCarGuids: [],
    },
    {
      guid: "22222222-2222-4222-8222-222222222222",
      state: "unresearchableNotEnoughPreNode",
      iconItemId: 3,
      position: { x: 300.0, y: -120.0 },
      prevGuids: ["11111111-1111-4111-8111-111111111111"],
      consumeItems: [],
      rewardItems: [],
      unlockItemIds: [3],
      unlockBlocks: [],
      unlockMachineRecipeOutputItemIds: [],
      unlockConnectToolGuids: [],
      unlockTrainCarGuids: [],
    },
    {
      guid: researchableNodeGuid,
      state: "researchable",
      iconItemId: 100,
      position: { x: 600.0, y: 0.0 },
      prevGuids: ["11111111-1111-4111-8111-111111111111"],
      consumeItems: [{ itemId: 1, count: 5 }],
      rewardItems: [{ itemId: 100, count: 2 }],
      unlockItemIds: [],
      unlockBlocks: [{ blockId: 1, blockGuid: "44444444-4444-4444-8444-444444444444" }],
      unlockMachineRecipeOutputItemIds: [2],
      unlockConnectToolGuids: ["55555555-5555-4555-8555-555555555555"],
      unlockTrainCarGuids: ["66666666-6666-4666-8666-666666666666"],
    },
    // 第4ノード: 前提充足・アイテム不足(mockインベントリはitemId1を計15個しか持たない)
    // Fourth node: prerequisites met but items lacking (the mock inventory owns only 15 of itemId 1)
    {
      guid: itemLackingNodeGuid,
      state: "unresearchableNotEnoughItem",
      iconItemId: 2,
      position: { x: 600.0, y: -240.0 },
      prevGuids: ["11111111-1111-4111-8111-111111111111"],
      consumeItems: [{ itemId: 1, count: 999 }],
      rewardItems: [],
      unlockItemIds: [],
      unlockBlocks: [],
      unlockMachineRecipeOutputItemIds: [],
      unlockConnectToolGuids: [],
      unlockTrainCarGuids: [],
    },
  ],
} satisfies ResearchTreeData;
