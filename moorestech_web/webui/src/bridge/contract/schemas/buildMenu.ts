import { z } from "zod";

export const BuildMenuEntryTypeSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);

export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
});

export const BuildMenuEntryDataSchema = z.object({
  entryType: BuildMenuEntryTypeSchema,
  entryKey: z.string(),
  label: z.string(),
  category: z.string(),
  subCategory: z.string(),
  requiredItems: z.array(BuildMenuRequiredItemSchema),
  iconUrl: z.string().optional(),
});

export const BuildMenuCategorySchema = z.object({
  name: z.string(),
  subCategories: z.array(z.string()),
});

export const BuildMenuDataSchema = z.object({
  categories: z.array(BuildMenuCategorySchema),
  entries: z.array(BuildMenuEntryDataSchema),
});
