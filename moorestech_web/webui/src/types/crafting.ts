// crafting.recipes トピックの手書き型
// Handwritten types for crafting.recipes

export type RequiredItem = { itemId: number; count: number };

export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};

export type CraftRecipesData = { recipes: CraftRecipe[] };
