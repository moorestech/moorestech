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
// MantineProvider依存（Tooltip等）を避けるためGamePanel/ItemSlot/BlockSlot/FluidSlotはスタブにする
// (UnlockSectionsがBlockSlot/FluidSlotをimportするため、解放物入りfixtureを使うにはここも必須)
// Stub GamePanel/ItemSlot/BlockSlot/FluidSlot to avoid MantineProvider dependencies (Tooltip, etc.)
// (UnlockSections imports BlockSlot/FluidSlot, so these are required once a fixture carries unlock entries)
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: unknown }) => createElement("mock-game-panel", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  BlockSlot: (props: object) => createElement("mock-block-slot", props),
  FluidSlot: (props: object) => createElement("mock-fluid-slot", props),
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
      node, owned: new Map([[1, 5]]), onClose: () => {},
    }));
    const button = renderer.root.findByProps({ "data-testid": `research-button-${researchGuid}` });
    expect(button.props.disabled).toBe(false);
    act(() => button.props.onClick());
    expect(dispatchMock).toHaveBeenCalledWith("research.complete", { researchGuid });
    const rendered = safeStringify(renderer.toJSON());
    expect(rendered).toContain(`research.${researchGuid}.name`);
    expect(rendered).toContain(`research.${researchGuid}.description`);
  });

  it("サーバーstateがアイテム不足ならボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる", () => {
    const onClose = vi.fn();
    const lackingNode: ResearchNodeData = { ...node, state: "unresearchableNotEnoughItem" };
    const renderer = create(createElement(ResearchDetailPane, {
      node: lackingNode, owned: new Map(), onClose,
    }));
    expect(renderer.root.findByProps({ "data-testid": `research-button-${researchGuid}` }).props.disabled).toBe(true);
    expect(renderer.root.findByProps({ "data-testid": "research-detail-reason" })).toBeTruthy();
    act(() => renderer.root.findByProps({ "data-testid": "research-detail-close" }).props.onClick());
    expect(onClose).toHaveBeenCalled();
  });

  it("所持数不足の消費アイテムはinsufficient=trueで所持/必要をスロットへ渡す", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map(), onClose: () => {},
    }));
    const slot = renderer.root.findByType("mock-item-slot" as never);
    expect(slot.props.insufficient).toBe(true);
    expect(slot.props.shortage).toEqual({ ownedCount: 0, requiredCount: 2, tooltipKey: "ui.research.consumeItemTooltip" });
  });

  it("所持数が足りていればinsufficient=falseで所持/必要をスロットへ渡す", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map([[1, 2]]), onClose: () => {},
    }));
    const slot = renderer.root.findByType("mock-item-slot" as never);
    expect(slot.props.insufficient).toBe(false);
    expect(slot.props.shortage).toEqual({ ownedCount: 2, requiredCount: 2, tooltipKey: "ui.research.consumeItemTooltip" });
  });

  it("所持数未受信中(owned=null)は不足表示も所持/必要も出さない", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: null, onClose: () => {},
    }));
    const slot = renderer.root.findByType("mock-item-slot" as never);
    expect(slot.props.insufficient).toBe(false);
    expect(slot.props.shortage).toBeUndefined();
  });

  it("完了ノードは所持不足でも消費アイテムを不足強調しない", () => {
    const completedNode: ResearchNodeData = { ...node, state: "completed" };
    const renderer = create(createElement(ResearchDetailPane, {
      node: completedNode, owned: new Map(), onClose: () => {},
    }));
    expect(renderer.root.findByType("mock-item-slot" as never).props.insufficient).toBe(false);
  });

  it("機械レシピはアイテム出力と液体出力を両方描く（混在レシピの液体を落とさない）", () => {
    const nodeWithRecipes: ResearchNodeData = {
      ...node,
      unlockMachineRecipes: [
        // アイテムと液体を同時に出すレシピ（排他分岐なら液体が消える）
        // A recipe emitting both an item and a fluid (an exclusive branch would drop the fluid)
        { recipeGuid: "86000000-0000-4000-8000-0000000000a1", outputItemIds: [11], outputFluids: [{ fluidId: 5, fluidGuid: "86000000-0000-4000-8000-0000000000f1", amount: 300 }] },
        // 液体のみのレシピ
        // A fluid-only recipe
        { recipeGuid: "86000000-0000-4000-8000-0000000000a2", outputItemIds: [], outputFluids: [{ fluidId: 6, fluidGuid: "86000000-0000-4000-8000-0000000000f2", amount: 100 }] },
      ],
    };
    const renderer = create(createElement(ResearchDetailPane, {
      node: nodeWithRecipes, owned: new Map([[1, 2]]), onClose: () => {},
    }));
    const section = renderer.root.findByProps({ "data-testid": "research-unlock-machine-recipes" });
    expect(section.findAllByType("mock-item-slot" as never)).toHaveLength(1);
    expect(section.findAllByProps({ "data-testid": "research-unlock-fluid" })).toHaveLength(2);
  });

  it("itemRecipeView解放とconnectTool/trainCarのテキスト行が並ぶ", () => {
    const nodeWithOthers: ResearchNodeData = {
      ...node,
      unlockItemRecipeViewItemIds: [21],
      unlockConnectToolGuids: ["86000000-0000-4000-8000-0000000000c1"],
      unlockTrainCarGuids: ["86000000-0000-4000-8000-0000000000c2"],
    };
    const renderer = create(createElement(ResearchDetailPane, {
      node: nodeWithOthers, owned: new Map([[1, 2]]), onClose: () => {},
    }));
    expect(renderer.root.findByProps({ "data-testid": "research-unlock-items" })
      .findAllByType("mock-item-slot" as never)).toHaveLength(1);
    expect(renderer.root.findByProps({ "data-testid": "research-unlock-others" })
      .findAllByType("p")).toHaveLength(2);
  });

  it("解放ブロックがあればBlockSlotで描画される", () => {
    const nodeWithUnlock: ResearchNodeData = {
      ...node,
      unlockBlocks: [{ blockId: 10, blockGuid: "86000000-0000-4000-8000-000000000099" }],
    };
    const renderer = create(createElement(ResearchDetailPane, {
      node: nodeWithUnlock, owned: new Map([[1, 2]]), onClose: () => {},
    }));
    expect(renderer.root.findAllByType("mock-block-slot" as never)).toHaveLength(1);
    expect(renderer.root.findByProps({ "data-testid": "research-unlock-blocks" })).toBeTruthy();
  });
});
