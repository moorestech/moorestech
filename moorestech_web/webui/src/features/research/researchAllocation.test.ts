import { describe, expect, it } from "vitest";
import type { ResearchNodeData } from "@/bridge";
import { allocateResearchItems } from "./researchLogic";

describe("research allocation", () => {
  it.each([false, true])("reserves exact variants regardless of requirement order: %s", (reverse) => {
    const base = { itemId: 1, count: 1, acceptedItemIds: [1, 2, 3] };
    const exact = { itemId: 2, count: 1, acceptedItemIds: [2] };
    const node = { consumeItems: reverse ? [exact, base] : [base, exact] } as ResearchNodeData;
    const owned = new Map([[2, 1]]);
    expect(allocateResearchItems(node, owned)).toEqual(reverse ? [1, 0] : [0, 1]);
    expect([...owned]).toEqual([[2, 1]]);
    owned.set(3, 1);
    expect(allocateResearchItems(node, owned)).toEqual([1, 1]);
  });
});
