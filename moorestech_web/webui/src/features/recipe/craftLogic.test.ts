import { describe, it, expect } from "vitest";
import {
  buildOwnedCounts,
  craftable,
  clampIndex,
  selectCraftRecipes,
  groupMachineRecipesByBlock,
  buildRecipeTabs,
} from "./craftLogic";
import type {
  PlayerInventoryData,
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
} from "@/bridge/contract/payloadTypes";

const craftRecipe = (resultItemId: number, guid: string): CraftRecipe => ({
  recipeGuid: guid,
  resultItemId,
  resultCount: 1,
  craftTime: 1,
  requiredItems: [],
});

const machineRecipe = (blockItemId: number, blockName: string, outputItemId: number, guid: string): MachineRecipe => ({
  recipeGuid: guid,
  blockItemId,
  blockName,
  time: 1,
  inputItems: [],
  outputItems: [{ itemId: outputItemId, count: 1 }],
});

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

describe("selectCraftRecipes", () => {
  const data: CraftRecipesData = { recipes: [craftRecipe(9, "a"), craftRecipe(5, "b"), craftRecipe(9, "c")] };
  it("resultItemId 一致のみ抽出する", () => {
    expect(selectCraftRecipes(data, 9).map((r) => r.recipeGuid)).toEqual(["a", "c"]);
  });
  it("一致無しは空配列", () => {
    expect(selectCraftRecipes(data, 42)).toEqual([]);
  });
});

describe("groupMachineRecipesByBlock", () => {
  const data: MachineRecipesData = {
    recipes: [
      machineRecipe(10, "Furnace", 9, "m1"),
      machineRecipe(10, "Furnace", 9, "m2"),
      machineRecipe(20, "Assembler", 9, "m3"),
      machineRecipe(20, "Assembler", 7, "m4"),
    ],
  };
  it("出力アイテム一致を blockItemId 毎に集約する", () => {
    const groups = groupMachineRecipesByBlock(data, 9);
    expect([...groups.keys()]).toEqual([10, 20]);
    expect(groups.get(10)!.map((r) => r.recipeGuid)).toEqual(["m1", "m2"]);
    expect(groups.get(20)!.map((r) => r.recipeGuid)).toEqual(["m3"]);
  });
  it("一致無しは空 Map", () => {
    expect(groupMachineRecipesByBlock(data, 999).size).toBe(0);
  });
});

describe("buildRecipeTabs", () => {
  it("クラフト有り→先頭が craft タブ、続いて機械タブ", () => {
    const groups = groupMachineRecipesByBlock(
      { recipes: [machineRecipe(10, "Furnace", 9, "m1")] },
      9,
    );
    const tabs = buildRecipeTabs([craftRecipe(9, "a")], groups);
    expect(tabs).toEqual([
      { key: "craft", label: "クラフト", blockItemId: null },
      { key: "m10", label: "Furnace", blockItemId: 10 },
    ]);
  });
  it("クラフト無し→機械タブのみ", () => {
    const groups = groupMachineRecipesByBlock(
      { recipes: [machineRecipe(20, "Assembler", 9, "m1")] },
      9,
    );
    const tabs = buildRecipeTabs([], groups);
    expect(tabs).toEqual([{ key: "m20", label: "Assembler", blockItemId: 20 }]);
  });
  it("両方無し→空配列", () => {
    expect(buildRecipeTabs([], new Map())).toEqual([]);
  });
});
