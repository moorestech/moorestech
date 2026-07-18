import type {
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
} from "@/bridge";
import { hasEnoughItems } from "@/shared/ownedCounts";

// null はクラフトタブ
// null denotes the craft tab
type RecipeTab = { key: string; label: string; blockId: number | null };

// 選択アイテムを生産するクラフトレシピを抽出する純関数。
// Pure selector for craft recipes that produce the selected item.
export function selectCraftRecipes(recipes: CraftRecipesData, itemId: number): CraftRecipe[] {
  return recipes.recipes.filter((r) => r.resultItemId === itemId);
}

export function groupMachineRecipesByBlock(
  machineRecipes: MachineRecipesData,
  itemId: number,
): Map<number, MachineRecipe[]> {
  const groups = new Map<number, MachineRecipe[]>();
  machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .forEach((r) => {
      const group = groups.get(r.blockId) ?? [];
      group.push(r);
      groups.set(r.blockId, group);
    });
  return groups;
}

// クラフトタブ + 機械ごとのタブを組み立てる純関数（uGUI RecipeTabView 相当）。
// Pure builder for the craft tab plus one tab per machine (mirrors uGUI RecipeTabView).
export function buildRecipeTabs(
  craftRecipes: CraftRecipe[],
  machineGroups: Map<number, MachineRecipe[]>,
): RecipeTab[] {
  const tabs: RecipeTab[] = [];
  if (craftRecipes.length > 0) tabs.push({ key: "craft", label: "クラフト", blockId: null });
  machineGroups.forEach((group, blockId) =>
    tabs.push({ key: `m${blockId}`, label: group[0].blockName, blockId }),
  );
  return tabs;
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
