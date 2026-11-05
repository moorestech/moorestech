import { describe, it, expect } from "vitest";

import { parseTopicPayload } from "./validators";
import { loadFixture } from "./wireFixtures.test-helper";
import { Topics } from "../transport/protocol";
import type { ResearchTreeData } from "./payloadTypes";

describe("research_tree fixture", () => {
  it("accepts and types research payload", () => {
    const data = loadFixture("research_tree.json");
    expect(parseTopicPayload(Topics.researchTree, data).valid).toBe(true);
    const tree = data as ResearchTreeData;
    expect(tree.nodes[0].iconItemId).toBe(2);
    expect(tree.nodes.length).toBe(2);
    expect(tree.nodes[1].prevGuids).toContain(tree.nodes[0].guid);
  });

  it("解放物4種のフィールドを受理し型消費できる", () => {
    const data = loadFixture("research_tree.json") as ResearchTreeData;
    expect(parseTopicPayload(Topics.researchTree, data).valid).toBe(true);
    const node = data.nodes[1];
    expect(node.unlockBlocks[0]).toEqual({ blockId: 7, blockGuid: "44444444-4444-4444-8444-444444444444" });
    expect(node.unlockMachineRecipes[0].outputItemIds).toEqual([9]);
    expect(node.unlockMachineRecipes[0].outputFluids).toEqual([
      { fluidId: 1, fluidGuid: "99999999-9999-4999-8999-999999999999", amount: 100 },
    ]);
    expect(node.unlockConnectToolGuids.length + node.unlockTrainCarGuids.length).toBe(2);
  });

  it("代替の受け付けIDと機械強化の解放2種を受理し、0以下の上限は拒否する", () => {
    const data = loadFixture("research_tree.json") as ResearchTreeData;
    expect(parseTopicPayload(Topics.researchTree, data).valid).toBe(true);
    expect(data.nodes[0].consumeItems[0].acceptedItemIds).toEqual([1, 11]);
    expect(data.nodes[1].unlockModuleSlotStackLimits).toEqual([6]);
    expect(data.nodes[1].unlockLevelInheritanceCraftCaps).toEqual([{ itemId: 21, level: 2 }]);

    const invalid = structuredClone(data);
    invalid.nodes[1].unlockLevelInheritanceCraftCaps[0].level = 0;
    expect(parseTopicPayload(Topics.researchTree, invalid).valid).toBe(false);
  });
});
