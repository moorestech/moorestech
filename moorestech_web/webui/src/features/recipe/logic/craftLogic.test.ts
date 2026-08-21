import { describe, it, expect } from "vitest";
import {
  craftable,
  buildRecipeEntries,
  craftableResultCounts,
} from "./craftLogic";
import type {
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
} from "@/bridge";

const recipeA = "88000000-0000-4000-8000-000000000001";
const recipeB = "88000000-0000-4000-8000-000000000002";
const recipeC = "88000000-0000-4000-8000-000000000003";

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

describe("buildRecipeEntries", () => {
  const craft = (guid: string, resultItemId: number): CraftRecipe => ({
    recipeGuid: guid, resultItemId, resultCount: 1, craftTime: 2,
    requiredItems: [{ itemId: 1, count: 1 }],
  });
  const machine = (guid: string, outputItemId: number, blockId: number): MachineRecipe => ({
    recipeGuid: guid, blockGuid: "00000000-0000-0000-0000-00000000000b", blockId, time: 4,
    inputItems: [{ itemId: 1, count: 1 }], outputItems: [{ itemId: outputItemId, count: 1 }],
  });

  it("クラフトレシピを先頭に、機械レシピを後ろにデータ順で並べる", () => {
    const entries = buildRecipeEntries(
      { recipes: [craft("c1", 9), craft("c2", 5), craft("c3", 9)] },
      { recipes: [machine("m1", 9, 100), machine("m2", 7, 100), machine("m3", 9, 200)] },
      9,
    );
    expect(entries.map((e) => e.recipe.recipeGuid)).toEqual(["c1", "c3", "m1", "m3"]);
    expect(entries.map((e) => e.kind)).toEqual(["craft", "craft", "machine", "machine"]);
  });

  it("対象アイテムのレシピが無ければ空配列", () => {
    expect(buildRecipeEntries({ recipes: [] }, { recipes: [] }, 9)).toEqual([]);
  });

  // クラフト絞り込み単体(旧関数から移植)
  // Craft filtering unit, migrated from the old function
  const craftOnly: CraftRecipesData = { recipes: [craftRecipe(9, recipeA), craftRecipe(5, recipeB), craftRecipe(9, recipeC)] };

  it("クラフトはresultItemId一致のみ抽出する", () => {
    const entries = buildRecipeEntries(craftOnly, { recipes: [] }, 9);

    expect(entries.map((e) => e.recipe.recipeGuid)).toEqual([recipeA, recipeC]);
    expect(entries.every((e) => e.kind === "craft")).toBe(true);
  });

  it("クラフトのresultItemIdが一致しなければ空配列", () => {
    expect(buildRecipeEntries(craftOnly, { recipes: [] }, 42)).toEqual([]);
  });
});
