import { z } from "zod";
import { GuidSchema } from "./common";

const ItemIdSchema = z.number().int().positive();
const ItemCountSchema = z.number().int().positive();

export const RequiredItemSchema = z.object({ itemId: ItemIdSchema, count: ItemCountSchema });
export const CraftRecipeSchema = z.object({
  recipeGuid: GuidSchema,
  resultItemId: ItemIdSchema,
  resultCount: ItemCountSchema,
  craftTime: z.number().nonnegative(),
  requiredItems: z.array(RequiredItemSchema),
});
export const CraftRecipesDataSchema = z.object({ recipes: z.array(CraftRecipeSchema) });

export const MachineRecipeItemSchema = z.object({ itemId: z.number(), count: z.number() });
// 名前・色はfluidGuidで解決するため、レシピ配信はGUIDと量だけを運ぶ
// Names and colors resolve from fluidGuid, so the recipe payload carries only the GUID and the amount
const MachineRecipeFluidSchema = z.object({
  fluidGuid: GuidSchema,
  amount: z.number(),
}).strict();
export const MachineRecipeSchema = z.object({
  recipeGuid: GuidSchema,
  blockGuid: GuidSchema,
  blockId: z.number(),
  time: z.number(),
  inputItems: z.array(MachineRecipeItemSchema),
  outputItems: z.array(MachineRecipeItemSchema),
  inputFluids: z.array(MachineRecipeFluidSchema),
  outputFluids: z.array(MachineRecipeFluidSchema),
}).strict();
export const MachineRecipesDataSchema = z.object({ recipes: z.array(MachineRecipeSchema) });
export const RecipeViewerItemListDataSchema = z.object({ itemIds: z.array(z.number()) });
export const ItemMasterEntrySchema = z.object({ itemId: z.number(), itemGuid: GuidSchema, maxStack: z.number() });
export const ItemMasterDataSchema = z.object({ items: z.array(ItemMasterEntrySchema) });

// colorはfluids.ymlのRRGGBB(#付き)を素通しする。D8: 背面フィル色はマスタ定義であり導出しない
// color passes through fluids.yml's #RRGGBB verbatim; per D8 the fill color is master-defined, never derived
export const FluidMasterEntrySchema = z.object({
  fluidGuid: GuidSchema,
  color: z.string().regex(/^#[0-9a-fA-F]{6}$/),
});
export const FluidMasterDataSchema = z.object({ fluids: z.array(FluidMasterEntrySchema) });
