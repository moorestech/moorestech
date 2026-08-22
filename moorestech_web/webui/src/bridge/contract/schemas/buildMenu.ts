import { z } from "zod";

// kind は表示・振る舞いの分類で識別子ではない。設置対象の同一性は id(Guid) だけが持つ
// kind classifies display/behavior only; identity of a placement target lives solely in id (a GUID)
export const BuildMenuEntryKindSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);

export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
});

const BuildMenuEntryCommonFields = {
  id: z.string().uuid(),
  categoryGuid: z.string().uuid(),
  subCategoryGuid: z.string().uuid(),
  requiredItems: z.array(BuildMenuRequiredItemSchema),
  iconUrl: z.string().optional(),
};

// 設置数系フィールドはブロック専用
// Placement-count fields belong to blocks alone
const BuildMenuBlockEntryDataSchema = z.object({
  kind: z.literal("block"),
  ...BuildMenuEntryCommonFields,
  placementsPerCost: z.number().int().min(1),
  remainingPlacementCount: z.number().int().min(0),
  label: z.never().optional(),
}).strict();

// マスタ由来名はGuid導出キーで解決し、ホストから表示名を運ばない
// Resolve master-derived names through GUID-derived keys without host-provided labels
const BuildMenuDictionaryResolvedEntryDataSchema = z.object({
  kind: z.enum(["trainCar", "connectTool"]),
  ...BuildMenuEntryCommonFields,
  label: z.never().optional(),
}).strict();

const BuildMenuBlueprintCopyEntryDataSchema = z.object({
  kind: z.literal("blueprintCopy"),
  ...BuildMenuEntryCommonFields,
  label: z.never().optional(),
}).strict();

const BuildMenuBlueprintEntryDataSchema = z.object({
  kind: z.literal("blueprint"),
  ...BuildMenuEntryCommonFields,
  label: z.string().min(1),
}).strict();

export const BuildMenuEntryDataSchema = z.discriminatedUnion("kind", [
  BuildMenuBlockEntryDataSchema,
  BuildMenuDictionaryResolvedEntryDataSchema,
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
