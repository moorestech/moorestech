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

// tooltipプロップに渡すJSX要素はdev用_ownerでFiberを循環参照するため、標準のJSON.stringifyでは落ちる。
// 祖先スタックだけを追跡し、真の循環(同一オブジェクトを子孫に持つ)だけを[Circular]化する
// JSX passed as the tooltip prop carries a dev-only _owner back to the Fiber, so plain JSON.stringify throws on the cycle.
// Track only the ancestor stack so genuine cycles (an object nested under itself) become [Circular]
function safeStringify(value: unknown): string {
  const ancestors: object[] = [];
  return JSON.stringify(value, function replacer(_key, val) {
    if (typeof val !== "object" || val === null) return val;
    while (ancestors.length && ancestors[ancestors.length - 1] !== this) ancestors.pop();
    if (ancestors.includes(val)) return "[Circular]";
    ancestors.push(val);
    return val;
  });
}

const researchGuid = "86000000-0000-4000-8000-000000000001";
const node: ResearchNodeData = {
  guid: researchGuid, state: "researchable", iconItemId: 1,
  position: { x: 0, y: 0 }, prevGuids: [], consumeItems: [{ itemId: 1, count: 2 }], rewardItems: [], unlockItemRecipeViewItemIds: [],
  unlockBlocks: [], unlockMachineRecipes: [], unlockConnectToolGuids: [], unlockTrainCarGuids: [],
};

describe("ResearchDetailPane", () => {
  it("研究可能ノードでボタン活性・クリックでresearch.completeを送る", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map([[1, 5]]), ownedKnown: true, onClose: () => {},
    }));
    const button = renderer.root.findByProps({ "data-testid": `research-button-${researchGuid}` });
    expect(button.props.disabled).toBe(false);
    act(() => button.props.onClick());
    expect(dispatchMock).toHaveBeenCalledWith("research.complete", { researchGuid });
    const rendered = safeStringify(renderer.toJSON());
    expect(rendered).toContain(`research.${researchGuid}.name`);
    expect(rendered).toContain(`research.${researchGuid}.description`);
  });

  it("不足時はボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる", () => {
    const onClose = vi.fn();
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map(), ownedKnown: true, onClose,
    }));
    expect(renderer.root.findByProps({ "data-testid": `research-button-${researchGuid}` }).props.disabled).toBe(true);
    expect(renderer.root.findByProps({ "data-testid": "research-detail-reason" })).toBeTruthy();
    act(() => renderer.root.findByProps({ "data-testid": "research-detail-close" }).props.onClick());
    expect(onClose).toHaveBeenCalled();
  });
});
