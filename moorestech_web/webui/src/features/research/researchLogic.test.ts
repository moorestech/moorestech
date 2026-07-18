import { describe, expect, it } from "vitest";
import {
  computeCanvasBounds,
  deriveResearchButton,
  hasEnoughItems,
  isItemSufficient,
  lineBetween,
  zoomViewportAt,
} from "./researchLogic";
import type { ResearchNodeData } from "@/bridge";

const node = (guid: string, x: number, y: number, extra?: Partial<ResearchNodeData>): ResearchNodeData => ({
  guid, name: guid, description: "", state: "researchable",
  position: { x, y }, prevGuids: [], consumeItems: [], rewardItemIds: [], unlockItemIds: [], ...extra,
});

describe("computeCanvasBounds", () => {
  it("flips node Y into CSS top via offsetY (uGUI Y-up to CSS Y-down)", () => {
    const b = computeCanvasBounds([node("a", 0, 0), node("b", 300, -120)]);
    expect(b).toEqual({ width: 700, height: 520, offsetX: 200, offsetY: 200 });
  });
  it("handles empty nodes", () => {
    expect(computeCanvasBounds([])).toEqual({ width: 400, height: 400, offsetX: 200, offsetY: 200 });
  });
});

describe("lineBetween", () => {
  it("computes length and angle from child to parent", () => {
    const line = lineBetween({ x: 0, y: 0 }, { x: 100, y: 0 });
    expect(line.length).toBeCloseTo(100);
    expect(line.angleDeg).toBeCloseTo(0);
    const diag = lineBetween({ x: 0, y: 0 }, { x: 0, y: 100 });
    expect(diag.angleDeg).toBeCloseTo(90);
  });
});

describe("zoomViewportAt", () => {
  it("zooms in for wheel-up while keeping the world point under the cursor fixed", () => {
    const current = { x: 40, y: -20, scale: 1 };
    const cursor = { x: 300, y: 180 };
    const next = zoomViewportAt(current, cursor, -120);
    const worldBefore = {
      x: (cursor.x - current.x) / current.scale,
      y: (cursor.y - current.y) / current.scale,
    };
    expect(next.scale).toBeGreaterThan(current.scale);
    expect((cursor.x - next.x) / next.scale).toBeCloseTo(worldBefore.x);
    expect((cursor.y - next.y) / next.scale).toBeCloseTo(worldBefore.y);
  });

  it("clamps wheel zoom to the supported minimum and maximum", () => {
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, -100000).scale).toBe(2.5);
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, 100000).scale).toBe(0.4);
  });
});

describe("hasEnoughItems", () => {
  it("checks owned counts against consume items", () => {
    const n = node("a", 0, 0, { consumeItems: [{ itemId: 1, count: 3 }] });
    expect(hasEnoughItems(n, new Map([[1, 3]]))).toBe(true);
    expect(hasEnoughItems(n, new Map([[1, 2]]))).toBe(false);
  });
});

describe("deriveResearchButton", () => {
  it("disables completed nodes and never highlights completed consume items", () => {
    const n = node("done", 0, 0, { state: "completed", consumeItems: [{ itemId: 1, count: 1 }] });
    const owned = new Map([[1, 1]]);
    expect(deriveResearchButton(n, owned)).toEqual({ completed: true, interactable: false, tooltip: "研究済み" });
    expect(isItemSufficient(n, 1, 1, owned)).toBe(false);
  });
  it("enables researchable nodes when all consume items are owned", () => {
    const n = node("ready", 0, 0, { consumeItems: [{ itemId: 1, count: 1 }] });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: true,
      tooltip: "クリックして研究",
    });
  });
  it("reports missing items when prerequisites are met but consume items are short", () => {
    const n = node("short", 0, 0, { consumeItems: [{ itemId: 1, count: 2 }] });
    expect(deriveResearchButton(n, new Map([[1, 1]]))).toEqual({
      completed: false,
      interactable: false,
      tooltip: "研究アイテムが足りません。",
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
      tooltip: "前提研究が完了していません。",
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
      tooltip: "研究アイテムが足りません。\n前提研究が完了していません。",
    });
  });
});
