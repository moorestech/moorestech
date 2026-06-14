import { describe, it, expect } from "vitest";
import { buildOwnedCounts, craftable, clampIndex } from "./craftLogic";
import type { PlayerInventoryData, CraftRecipe } from "@/bridge/payloadTypes";

const inv = (
  main: [number, number][],
  hot: [number, number][],
  grab: [number, number],
): PlayerInventoryData => ({
  mainSlots: main.map(([itemId, count]) => ({ itemId, count })),
  hotbarSlots: hot.map(([itemId, count]) => ({ itemId, count })),
  grab: { itemId: grab[0], count: grab[1] },
  selectedHotbar: 0,
});

describe("buildOwnedCounts", () => {
  it("main+hotbar を合算し grab を除外する", () => {
    const counts = buildOwnedCounts(inv([[1, 3]], [[1, 2]], [1, 99]));
    expect(counts.get(1)).toBe(5);
  });
  it("count 0 は加算しない", () => {
    const counts = buildOwnedCounts(inv([[0, 0]], [[2, 4]], [0, 0]));
    expect(counts.get(0)).toBeUndefined();
    expect(counts.get(2)).toBe(4);
  });
});

describe("craftable", () => {
  const recipe = {
    recipeGuid: "g",
    resultItemId: 9,
    resultCount: 1,
    craftTime: 1,
    requiredItems: [
      { itemId: 1, count: 2 },
      { itemId: 2, count: 1 },
    ],
  } satisfies CraftRecipe;
  it("全素材を満たせば true", () => {
    expect(craftable(recipe, new Map([[1, 2], [2, 1]]))).toBe(true);
  });
  it("一つでも不足なら false", () => {
    expect(craftable(recipe, new Map([[1, 1], [2, 1]]))).toBe(false);
  });
});

describe("clampIndex", () => {
  it("length 内に収める", () => {
    expect(clampIndex(5, 3)).toBe(2);
  });
  it("0 未満にしない", () => {
    expect(clampIndex(0, 0)).toBe(0);
  });
});
