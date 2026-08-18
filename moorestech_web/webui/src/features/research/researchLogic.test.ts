import { describe, expect, it } from "vitest";
import {
  deriveNodeCardState,
  deriveResearchButton,
  findInitialFocusNode,
  isItemSufficient,
} from "./researchLogic";
import type { ResearchNodeData } from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";
import { L } from "@/shared/i18n";

const node = (guid: string, x: number, y: number, extra?: Partial<ResearchNodeData>): ResearchNodeData => ({
  guid, state: "researchable", iconItemId: 1,
  position: { x, y }, prevGuids: [], consumeItems: [], rewardItems: [], unlockItemIds: [], ...extra,
});

describe("findInitialFocusNode", () => {
  it("researchableを最優先で選ぶ", () => {
    const nodes = [
      node("done", 0, 0, { state: "completed" }),
      node("lacking", 100, 0, { state: "unresearchableNotEnoughItem" }),
      node("ready", 200, 0, { state: "researchable" }),
    ];
    expect(findInitialFocusNode(nodes)?.guid).toBe("ready");
  });
  it("researchable不在なら素材待ちの最前線へフォールバックする", () => {
    const nodes = [
      node("done", 0, 0, { state: "completed" }),
      node("locked", 100, 0, { state: "unresearchableNotEnoughPreNode" }),
      node("lacking", 200, 0, { state: "unresearchableNotEnoughItem" }),
    ];
    expect(findInitialFocusNode(nodes)?.guid).toBe("lacking");
  });
  it("対象が無ければnullを返す", () => {
    expect(findInitialFocusNode([node("done", 0, 0, { state: "completed" })])).toBeNull();
    expect(findInitialFocusNode([])).toBeNull();
  });
});

describe("hasEnoughItems", () => {
  it("checks owned counts against consume items", () => {
    const n = node("a", 0, 0, { consumeItems: [{ itemId: 1, count: 3 }] });
    expect(hasEnoughItems(n.consumeItems, new Map([[1, 3]]))).toBe(true);
    expect(hasEnoughItems(n.consumeItems, new Map([[1, 2]]))).toBe(false);
  });
});

describe("deriveResearchButton", () => {
  it("disables completed nodes and never highlights completed consume items", () => {
    const n = node("done", 0, 0, { state: "completed", consumeItems: [{ itemId: 1, count: 1 }] });
    const owned = new Map([[1, 1]]);
    expect(deriveResearchButton(n, owned)).toEqual({
      completed: true,
      interactable: false,
      tooltipKey: L.ui.research.completed,
    });
    expect(isItemSufficient(n, 1, 1, owned)).toBe(false);
  });
  it("enables researchable nodes when all consume items are owned", () => {
    const n = node("ready", 0, 0, { consumeItems: [{ itemId: 1, count: 1 }] });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: true,
      tooltipKey: L.ui.research.clickToResearch,
    });
  });
  it("reports missing items when prerequisites are met but consume items are short", () => {
    const n = node("short", 0, 0, { consumeItems: [{ itemId: 1, count: 2 }] });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: false,
      tooltipKey: L.ui.research.missingItems,
    });
  });
  it("reports missing prerequisites when items are sufficient", () => {
    const n = node("locked", 0, 0, {
      state: "unresearchableNotEnoughPreNode",
      consumeItems: [{ itemId: 1, count: 1 }],
    });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: false,
      tooltipKey: L.ui.research.missingPrerequisites,
    });
  });
  it("reports both missing items and prerequisites", () => {
    const n = node("blocked", 0, 0, {
      state: "unresearchableAllReasons",
      consumeItems: [{ itemId: 1, count: 2 }],
    });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: false,
      tooltipKey: L.ui.research.missingItemsAndPrerequisites,
    });
  });
});

describe("deriveNodeCardState", () => {
  const owned = new Map([[1, 5]]);
  it("完了ノードはcompletedのみ立つ", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "completed" }), owned))
      .toEqual({ completed: true, ready: false, locked: false });
  });
  it("前提未達はlocked", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "unresearchableNotEnoughPreNode" }), owned))
      .toEqual({ completed: false, ready: false, locked: true });
  });
  it("前提充足でも所持不足ならready無しの通常表示", () => {
    const n = node("a", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 6 }] });
    expect(deriveNodeCardState(n, owned)).toEqual({ completed: false, ready: false, locked: false });
  });
  it("サーバーstateがアイテム不足でも所持が満ちればready（ライブ再計算）", () => {
    const n = node("a", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 5 }] });
    expect(deriveNodeCardState(n, owned)).toEqual({ completed: false, ready: true, locked: false });
  });
});
