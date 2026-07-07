import type {
  CraftRecipe,
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
} from "@/bridge/contract/payloadTypes";

// タブ定義。blockItemId が null ならクラフトレシピのタブ
// Tab descriptor; blockItemId null means the craft recipe tab
export type RecipeTab = { key: string; label: string; blockItemId: number | null };

// 選択アイテムを生産するクラフトレシピを抽出する純関数。
// Pure selector for craft recipes that produce the selected item.
export function selectCraftRecipes(recipes: CraftRecipesData, itemId: number): CraftRecipe[] {
  return recipes.recipes.filter((r) => r.resultItemId === itemId);
}

// 選択アイテムを出力する機械レシピを blockItemId 毎に集約する純関数。
// Pure grouping of machine recipes producing the item, keyed by blockItemId.
export function groupMachineRecipesByBlock(
  machineRecipes: MachineRecipesData,
  itemId: number,
): Map<number, MachineRecipe[]> {
  const groups = new Map<number, MachineRecipe[]>();
  machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .forEach((r) => {
      const group = groups.get(r.blockItemId) ?? [];
      group.push(r);
      groups.set(r.blockItemId, group);
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
  if (craftRecipes.length > 0) tabs.push({ key: "craft", label: "クラフト", blockItemId: null });
  machineGroups.forEach((group, blockItemId) =>
    tabs.push({ key: `m${blockItemId}`, label: group[0].blockName, blockItemId }),
  );
  return tabs;
}

// 全必要素材を所持数が満たすか。
// Whether owned counts satisfy every required material.
export function craftable(recipe: CraftRecipe, counts: Map<number, number>): boolean {
  return recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);
}

// recipeIndex を [0, length-1] にクランプ。呼び出し側が length>0 を保証する契約。
// Clamp recipeIndex into [0, length-1]; caller must guarantee length>0.
export function clampIndex(index: number, length: number): number {
  return Math.max(0, Math.min(index, length - 1));
}
