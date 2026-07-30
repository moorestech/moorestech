import { z } from "zod";

export const BuildMenuEntryTypeSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);

export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
});

const BuildMenuEntryCommonFields = {
  categoryGuid: z.string().uuid(),
  subCategoryGuid: z.string().uuid(),
  requiredItems: z.array(BuildMenuRequiredItemSchema),
  iconUrl: z.string().optional(),
};

const BuildMenuBlockEntryDataSchema = z.object({
  entryType: z.literal("block"),
  entryKey: z.string().uuid(),
  ...BuildMenuEntryCommonFields,
  label: z.never().optional(),
});

const BuildMenuGuidLabeledEntryDataSchema = z.object({
  entryType: z.enum(["trainCar", "connectTool"]),
  entryKey: z.string().uuid(),
  ...BuildMenuEntryCommonFields,
  label: z.string(),
});

const BuildMenuBlueprintCopyEntryDataSchema = z.object({
  entryType: z.literal("blueprintCopy"),
  entryKey: z.literal(""),
  ...BuildMenuEntryCommonFields,
  label: z.never().optional(),
});

const BuildMenuBlueprintEntryDataSchema = z.object({
  entryType: z.literal("blueprint"),
  entryKey: z.string().min(1),
  ...BuildMenuEntryCommonFields,
  label: z.string(),
});

export const BuildMenuEntryDataSchema = z.discriminatedUnion("entryType", [
  BuildMenuBlockEntryDataSchema,
  BuildMenuGuidLabeledEntryDataSchema,
  BuildMenuBlueprintCopyEntryDataSchema,
  BuildMenuBlueprintEntryDataSchema,
]);

export const BuildMenuCategorySchema = z.object({
  categoryGuid: z.string().uuid(),
  subCategoryGuids: z.array(z.string().uuid()),
});

export const BuildMenuDataSchema = z.object({
  categories: z.array(BuildMenuCategorySchema),
  entries: z.array(BuildMenuEntryDataSchema),
});
