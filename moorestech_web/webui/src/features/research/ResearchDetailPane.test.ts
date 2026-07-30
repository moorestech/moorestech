import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { ResearchNodeData } from "@/bridge";

const dispatchMock = vi.hoisted(() => vi.fn());
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: dispatchMock,
  useItemMaster: () => null,
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
// MantineProvider依存（Tooltip等）を避けるためGamePanel/ItemSlotはスタブにする
// Stub GamePanel/ItemSlot to avoid MantineProvider dependencies (Tooltip, etc.)
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: unknown }) => createElement("mock-game-panel", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
}));

import ResearchDetailPane from "./ResearchDetailPane";

const node: ResearchNodeData = {
  guid: "research-a", state: "researchable", iconItemId: 1,
  position: { x: 0, y: 0 }, prevGuids: [], consumeItems: [{ itemId: 1, count: 2 }], rewardItems: [], unlockItemIds: [],
};

describe("ResearchDetailPane", () => {
  it("研究可能ノードでボタン活性・クリックでresearch.completeを送る", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map([[1, 5]]), onClose: () => {},
    }));
    const button = renderer.root.findByProps({ "data-testid": "research-button-research-a" });
    expect(button.props.disabled).toBe(false);
    act(() => button.props.onClick());
    expect(dispatchMock).toHaveBeenCalledWith("research.complete", { researchGuid: "research-a" });
    const rendered = JSON.stringify(renderer.toJSON());
    expect(rendered).toContain("research.research-a.name");
    expect(rendered).toContain("research.research-a.description");
  });

  it("不足時はボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる", () => {
    const onClose = vi.fn();
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map(), onClose,
    }));
    expect(renderer.root.findByProps({ "data-testid": "research-button-research-a" }).props.disabled).toBe(true);
    expect(renderer.root.findByProps({ "data-testid": "research-detail-reason" })).toBeTruthy();
    act(() => renderer.root.findByProps({ "data-testid": "research-detail-close" }).props.onClick());
    expect(onClose).toHaveBeenCalled();
  });
});
