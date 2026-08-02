import { z } from "zod";

// kind は表示・振る舞いの分類で識別子ではない。設置対象の同一性は id(Guid) だけが持つ
// kind classifies display/behavior only; identity of a placement target lives solely in id (a GUID)
export const BuildMenuEntryKindSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);

export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
});

export const BuildMenuEntryDataSchema = z.object({
  id: z.string(),
  kind: BuildMenuEntryKindSchema,
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
