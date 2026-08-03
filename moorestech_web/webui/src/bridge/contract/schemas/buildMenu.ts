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

// Guid導出キーで辞書解決する種別。ホストは表示名を運ばない
// Kinds resolved from Guid-derived dictionary keys; the host never carries display names
const BuildMenuDictionaryResolvedEntryDataSchema = z.object({
  entryType: z.enum(["block", "connectTool"]),
  entryKey: z.string().uuid(),
  ...BuildMenuEntryCommonFields,
  label: z.never().optional(),
});

// 正準sourceが未定のためホストのLabelを維持する種別
// Kind that still carries a host label because its canonical source text is undecided
const BuildMenuTrainCarEntryDataSchema = z.object({
  entryType: z.literal("trainCar"),
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
  BuildMenuDictionaryResolvedEntryDataSchema,
  BuildMenuTrainCarEntryDataSchema,
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
