import { describe, expect, it } from "vitest";
import {
  deriveNodeCardState,
  deriveResearchButton,
  findInitialFocusNode,
  isConsumeItemLacking,
} from "./researchLogic";
import type { ResearchNodeData } from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";
import { L } from "@/shared/i18n";

const node = (guid: string, x: number, y: number, extra?: Partial<ResearchNodeData>): ResearchNodeData => ({
  guid, state: "researchable", iconItemId: 1,
  position: { x, y }, prevGuids: [], consumeItems: [], rewardItems: [], unlockItemRecipeViewItemIds: [],
  unlockBlocks: [], unlockMachineRecipes: [], unlockConnectToolGuids: [], unlockTrainCarGuids: [],
  ...extra,
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
    expect(deriveResearchButton(n)).toEqual({
      completed: true,
      interactable: false,
      tooltipKey: L.ui.research.completed,
    });
    expect(isConsumeItemLacking(n, 1, 1, new Map<number, number>())).toBe(false);
  });
  it("researchableは所持数を問わず活性（充足の正本はサーバーstate）", () => {
    const n = node("ready", 0, 0, { consumeItems: [{ itemId: 1, count: 999 }] });
    expect(deriveResearchButton(n)).toEqual({
      completed: false,
      interactable: true,
      tooltipKey: L.ui.research.clickToResearch,
    });
  });
  it("reports missing items when prerequisites are met but consume items are short", () => {
    const n = node("short", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 2 }] });
    expect(deriveResearchButton(n)).toEqual({
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
    expect(deriveResearchButton(n)).toEqual({
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
    expect(deriveResearchButton(n)).toEqual({
      completed: false,
      interactable: false,
      tooltipKey: L.ui.research.missingItemsAndPrerequisites,
    });
  });
});

describe("isConsumeItemLacking", () => {
  it("所持数が判明していれば不足を検出する", () => {
    const n = node("ready", 0, 0, { consumeItems: [{ itemId: 1, count: 2 }] });
    expect(isConsumeItemLacking(n, 1, 2, new Map([[1, 1]]))).toBe(true);
    expect(isConsumeItemLacking(n, 1, 2, new Map([[1, 2]]))).toBe(false);
  });
  it("所持数未受信(null)の間は不足を出さない(D4)", () => {
    // 空Mapを所持0と読み違えない
    // An empty map is never misread as "owns zero"
    const n = node("ready", 0, 0, { consumeItems: [{ itemId: 1, count: 1 }] });
    expect(isConsumeItemLacking(n, 1, 1, null)).toBe(false);
  });
});

describe("deriveNodeCardState", () => {
  it("完了ノードはcompletedのみ立つ", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "completed" })))
      .toEqual({ completed: true, ready: false, locked: false });
  });
  it("前提未達はlocked", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "unresearchableNotEnoughPreNode" })))
      .toEqual({ completed: false, ready: false, locked: true });
  });
  it("アイテム不足はready無しの通常表示", () => {
    const n = node("a", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 6 }] });
    expect(deriveNodeCardState(n)).toEqual({ completed: false, ready: false, locked: false });
  });
  it("全条件未達はlocked", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "unresearchableAllReasons" })))
      .toEqual({ completed: false, ready: false, locked: true });
  });
  it("researchableはready", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "researchable" })))
      .toEqual({ completed: false, ready: true, locked: false });
  });
});
