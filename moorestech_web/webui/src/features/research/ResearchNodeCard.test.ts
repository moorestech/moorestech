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

// 状態ラベルは本番DOMのtestidで引く（兄弟テストResearchDetailPane.test.tsと同じ引き方）
// The state label is pulled by the production testid, matching the sibling ResearchDetailPane.test.ts
function stateLabelOf(card: ReturnType<typeof renderCard>) {
  return card.findByProps({ "data-testid": `research-node-state-${guid}` });
}

describe("ResearchNodeCard", () => {
  it("状態ラベルはnodeState classNameでアイコン直下に描く", () => {
    const card = renderCard("completed");
    const [nameEl, itemEl] = card.children as unknown as Array<{ type: { name?: string }; props: Record<string, unknown> }>;
    expect(nameEl.props.className).toBe("nodeName");
    expect(itemEl.type.name).toBe("ItemSlot");
    expect(stateLabelOf(card).props.className).toBe("nodeState");
  });
  type FrameFlags = { ready?: true; completed?: true; locked?: true };
  it.each<[ResearchNodeData["state"], FrameFlags, string]>([
    ["researchable", { ready: true }, "ui.research.stateAvailable"],
    ["completed", { completed: true }, "ui.research.completed"],
    ["unresearchableNotEnoughPreNode", { locked: true }, "ui.research.stateUnavailable"],
    ["unresearchableNotEnoughItem", {}, "ui.research.stateUnavailable"],
    ["unresearchableAllReasons", { locked: true }, "ui.research.stateUnavailable"],
  ])("枠色用のdata属性と状態ラベルは%sで揃って付く", (state, expected, labelKey) => {
    const card = renderCard(state);
    expect(card.props["data-ready"]).toBe(expected.ready);
    expect(card.props["data-completed"]).toBe(expected.completed);
    expect(card.props["data-locked"]).toBe(expected.locked);
    expect(stateLabelOf(card).props.children).toBe(labelKey);
  });
});
