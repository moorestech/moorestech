import type {
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
} from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";

// 単一リストの1件=1レシピ
// One list entry maps to one recipe
export type RecipeEntry =
  | { kind: "craft"; recipe: CraftRecipe }
  | { kind: "machine"; recipe: MachineRecipe };

// 選択アイテムを生産するクラフトレシピを抽出する純関数。
// Pure selector for craft recipes that produce the selected item.
function selectCraftRecipes(recipes: CraftRecipesData, itemId: number): CraftRecipe[] {
  return recipes.recipes.filter((r) => r.resultItemId === itemId);
}

// 全レシピをクラフト優先の単一列へ畳む
// Flattens every recipe into one craft-first list
export function buildRecipeEntries(
  recipes: CraftRecipesData,
  machineRecipes: MachineRecipesData,
  itemId: number,
): RecipeEntry[] {
  const craftEntries: RecipeEntry[] = selectCraftRecipes(recipes, itemId)
    .map((recipe) => ({ kind: "craft", recipe }));
  const machineEntries: RecipeEntry[] = machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .map((recipe) => ({ kind: "machine", recipe }));
  return [...craftEntries, ...machineEntries];
}

// 全必要素材を所持数が満たすか。
// Whether owned counts satisfy every required material.
export function craftable(recipe: CraftRecipe, counts: Map<number, number>): boolean {
  return hasEnoughItems(recipe.requiredItems, counts);
}

// 完成品ごとの最大制作数を集計する
// Aggregate the maximum craftable result count per output item, matching uGUI ItemListView
export function craftableResultCounts(recipes: CraftRecipe[], counts: Map<number, number>): Map<number, number> {
  const result = new Map<number, number>();
  for (const recipe of recipes) {
    let times = Number.MAX_SAFE_INTEGER;
    for (const required of recipe.requiredItems) {
      times = Math.min(times, Math.floor((counts.get(required.itemId) ?? 0) / required.count));
    }
    const outputCount = times === Number.MAX_SAFE_INTEGER ? 0 : times * recipe.resultCount;
    if (outputCount > (result.get(recipe.resultItemId) ?? 0)) result.set(recipe.resultItemId, outputCount);
  }
  return result;
}
