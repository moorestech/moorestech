import { z } from "zod";

export const RequiredItemSchema = z.object({ itemId: z.number(), count: z.number() });
export const CraftRecipeSchema = z.object({
  recipeGuid: z.string(),
  resultItemId: z.number(),
  resultCount: z.number(),
  craftTime: z.number(),
  requiredItems: z.array(RequiredItemSchema),
});
export const CraftRecipesDataSchema = z.object({ recipes: z.array(CraftRecipeSchema) });

export const MachineRecipeItemSchema = z.object({ itemId: z.number(), count: z.number() });
export const MachineRecipeSchema = z.object({
  recipeGuid: z.string(),
  blockId: z.number(),
  blockName: z.string(),
  time: z.number(),
  inputItems: z.array(MachineRecipeItemSchema),
  outputItems: z.array(MachineRecipeItemSchema),
});
export const MachineRecipesDataSchema = z.object({ recipes: z.array(MachineRecipeSchema) });
export const RecipeViewerItemListDataSchema = z.object({ itemIds: z.array(z.number()) });
export const ItemMasterEntrySchema = z.object({ itemId: z.number(), name: z.string(), maxStack: z.number() });
export const ItemMasterDataSchema = z.object({ items: z.array(ItemMasterEntrySchema) });
