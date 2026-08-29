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

function renderStateText(state: ResearchNodeData["state"]): string {
  const renderer = create(createElement(ResearchNodeCard, { node: node(state), left: 0, top: 0, selected: false }));
  return renderer.root.findByProps({ "data-testid": `research-node-state-${guid}` }).props.children;
}

describe("ResearchNodeCard", () => {
  it("状態ラベルを完了済み/研究可能/研究不可の3語で描く", () => {
    expect(renderStateText("completed")).toBe("ui.research.stateCompleted");
    expect(renderStateText("researchable")).toBe("ui.research.stateAvailable");
    expect(renderStateText("unresearchableNotEnoughItem")).toBe("ui.research.stateUnavailable");
    expect(renderStateText("unresearchableNotEnoughPreNode")).toBe("ui.research.stateUnavailable");
  });
  it("枠色用のdata属性は従来どおり付く", () => {
    const renderer = create(createElement(ResearchNodeCard, { node: node("researchable"), left: 0, top: 0, selected: false }));
    const card = renderer.root.findByProps({ "data-testid": `research-node-${guid}` });
    expect(card.props["data-ready"]).toBe(true);
    expect(card.props["data-completed"]).toBeUndefined();
  });
});
