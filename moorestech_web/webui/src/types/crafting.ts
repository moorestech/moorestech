// crafting.recipes / crafting.machine_recipes トピックの手書き型
// Handwritten types for crafting.recipes / crafting.machine_recipes

export type RequiredItem = { itemId: number; count: number };

export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};

export type CraftRecipesData = { recipes: CraftRecipe[] };

export type MachineRecipeItem = { itemId: number; count: number };

export type MachineRecipe = {
  recipeGuid: string;
  blockItemId: number;
  blockName: string;
  time: number;
  inputItems: MachineRecipeItem[];
  outputItems: MachineRecipeItem[];
};

export type MachineRecipesData = { recipes: MachineRecipe[] };
