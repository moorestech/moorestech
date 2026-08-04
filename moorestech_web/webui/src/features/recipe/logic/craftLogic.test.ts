import { describe, it, expect } from "vitest";
import {
  craftable,
  selectCraftRecipes,
  groupMachineRecipesByBlock,
  buildRecipeTabs,
  craftableResultCounts,
} from "./craftLogic";
import type {
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
} from "@/bridge";
import { blockNameKey, L } from "@/shared/i18n";

const recipeA = "88000000-0000-4000-8000-000000000001";
const recipeB = "88000000-0000-4000-8000-000000000002";
const recipeC = "88000000-0000-4000-8000-000000000003";
const machineRecipeA = "89000000-0000-4000-8000-000000000001";
const machineRecipeB = "89000000-0000-4000-8000-000000000002";
const machineRecipeC = "89000000-0000-4000-8000-000000000003";
const machineRecipeD = "89000000-0000-4000-8000-000000000004";
const blockA = "8a000000-0000-4000-8000-000000000001";
const blockB = "8a000000-0000-4000-8000-000000000002";

const craftRecipe = (resultItemId: number, guid: string): CraftRecipe => ({
  recipeGuid: guid,
  resultItemId,
  resultCount: 1,
  craftTime: 1,
  requiredItems: [],
});

describe("craftableResultCounts", () => {
  it("素材の最小商に完成個数を掛け、同じ完成品は最大値を採用する", () => {
    const recipes: CraftRecipesData = { recipes: [
      { ...craftRecipe(9, recipeA), resultCount: 2, requiredItems: [{ itemId: 1, count: 3 }] },
      { ...craftRecipe(9, recipeB), resultCount: 1, requiredItems: [{ itemId: 2, count: 2 }] },
      { ...craftRecipe(8, recipeC), requiredItems: [{ itemId: 3, count: 1 }] },
    ] };

    expect(craftableResultCounts(recipes.recipes, new Map([[1, 7], [2, 10]]))).toEqual(new Map([[9, 5]]));
  });
});

const machineRecipe = (blockId: number, blockGuid: string, outputItemId: number, guid: string): MachineRecipe => ({
  recipeGuid: guid,
  blockGuid,
  blockId,
  time: 1,
  inputItems: [],
  outputItems: [{ itemId: outputItemId, count: 1 }],
});

describe("craftable", () => {
  const recipe = {
    recipeGuid: recipeA,
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

describe("selectCraftRecipes", () => {
  const data: CraftRecipesData = { recipes: [craftRecipe(9, recipeA), craftRecipe(5, recipeB), craftRecipe(9, recipeC)] };
  it("resultItemId 一致のみ抽出する", () => {
    expect(selectCraftRecipes(data, 9).map((r) => r.recipeGuid)).toEqual([recipeA, recipeC]);
  });
  it("一致無しは空配列", () => {
    expect(selectCraftRecipes(data, 42)).toEqual([]);
  });
});

describe("groupMachineRecipesByBlock", () => {
  const data: MachineRecipesData = {
    recipes: [
      machineRecipe(10, blockA, 9, machineRecipeA),
      machineRecipe(10, blockA, 9, machineRecipeB),
      machineRecipe(20, blockB, 9, machineRecipeC),
      machineRecipe(20, blockB, 7, machineRecipeD),
    ],
  };
  it("出力アイテム一致を blockId 毎に集約する", () => {
    const groups = groupMachineRecipesByBlock(data, 9);
    expect([...groups.keys()]).toEqual([10, 20]);
    expect(groups.get(10)!.map((r) => r.recipeGuid)).toEqual([machineRecipeA, machineRecipeB]);
    expect(groups.get(20)!.map((r) => r.recipeGuid)).toEqual([machineRecipeC]);
  });
  it("一致無しは空 Map", () => {
    expect(groupMachineRecipesByBlock(data, 999).size).toBe(0);
  });
});

describe("buildRecipeTabs", () => {
  it("クラフト有り→先頭が craft タブ、続いて機械タブ", () => {
    const groups = groupMachineRecipesByBlock(
      { recipes: [machineRecipe(10, blockA, 9, machineRecipeA)] },
      9,
    );
    const tabs = buildRecipeTabs([craftRecipe(9, recipeA)], groups);
    expect(tabs).toEqual([
      { key: "craft", labelKey: L.ui.recipe.craftTab, blockId: null },
      { key: "m10", labelKey: blockNameKey(blockA), blockId: 10 },
    ]);
  });
  it("クラフト無し→機械タブのみ", () => {
    const groups = groupMachineRecipesByBlock(
      { recipes: [machineRecipe(20, blockB, 9, machineRecipeA)] },
      9,
    );
    const tabs = buildRecipeTabs([], groups);
    expect(tabs).toEqual([{ key: "m20", labelKey: blockNameKey(blockB), blockId: 20 }]);
  });
  it("両方無し→空配列", () => {
    expect(buildRecipeTabs([], new Map())).toEqual([]);
  });
});
