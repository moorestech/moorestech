import { z } from "zod";

export const BuildMenuEntryTypeSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);
export const BuildMenuEntryDataSchema = z.object({
  entryType: BuildMenuEntryTypeSchema,
  entryKey: z.string(),
  label: z.string(),
  tooltip: z.string(),
  iconUrl: z.string().optional(),
});
export const BuildMenuDataSchema = z.object({ entries: z.array(BuildMenuEntryDataSchema) });
