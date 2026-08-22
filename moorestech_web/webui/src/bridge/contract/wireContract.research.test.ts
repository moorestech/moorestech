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
});
