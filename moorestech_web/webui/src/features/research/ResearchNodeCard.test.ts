import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { ResearchNodeData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
// ItemSlotはMantineProvider依存のためスタブ
// ItemSlot depends on MantineProvider, so stub it
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
}));

import ResearchNodeCard from "./ResearchNodeCard";

const guid = "86000000-0000-4000-8000-000000000002";
const node = (state: ResearchNodeData["state"]): ResearchNodeData => ({
  guid, state, iconItemId: 1,
  position: { x: 0, y: 0 }, prevGuids: [], consumeItems: [], rewardItems: [], unlockItemRecipeViewItemIds: [],
  unlockBlocks: [], unlockMachineRecipes: [], unlockConnectToolGuids: [], unlockTrainCarGuids: [],
});

function renderCard(state: ResearchNodeData["state"]) {
  const renderer = create(createElement(ResearchNodeCard, { node: node(state), left: 0, top: 0, selected: false }));
  return renderer.root.findByProps({ "data-testid": `research-node-${guid}` });
}

describe("ResearchNodeCard", () => {
  it("状態ラベルはnodeState classNameでアイコン直下に描く", () => {
    const card = renderCard("completed");
    const [nameEl, itemEl, stateEl] = card.children as unknown as Array<{ type: { name?: string }; props: Record<string, unknown> }>;
    expect(nameEl.props.className).toBe("nodeName");
    expect(itemEl.type.name).toBe("ItemSlot");
    expect(stateEl.props.className).toBe("nodeState");
    expect(stateEl.props.children).toBe("ui.research.stateCompleted");
  });
  type FrameFlags = { ready?: true; completed?: true; locked?: true };
  it.each<[ResearchNodeData["state"], FrameFlags]>([
    ["researchable", { ready: true }],
    ["completed", { completed: true }],
    ["unresearchableNotEnoughPreNode", { locked: true }],
    ["unresearchableNotEnoughItem", {}],
    ["unresearchableAllReasons", { locked: true }],
  ])("枠色用のdata属性は%sで従来どおり付く", (state, expected) => {
    const card = renderCard(state);
    expect(card.props["data-ready"]).toBe(expected.ready);
    expect(card.props["data-completed"]).toBe(expected.completed);
    expect(card.props["data-locked"]).toBe(expected.locked);
  });
});
