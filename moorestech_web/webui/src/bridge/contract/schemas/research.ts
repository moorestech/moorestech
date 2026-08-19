import { z } from "zod";
import { GuidSchema } from "./common";

export const ResearchNodeStateSchema = z.enum([
  "completed",
  "researchable",
  "unresearchableNotEnoughItem",
  "unresearchableNotEnoughPreNode",
  "unresearchableAllReasons",
]);
const ResearchUnlockFluidSchema = z.object({
  fluidId: z.number(),
  amount: z.number(),
  fluidGuid: GuidSchema.or(z.literal("")),
}).strict();
const ResearchUnlockMachineRecipeSchema = z.object({
  recipeGuid: GuidSchema,
  outputItemIds: z.array(z.number()),
  outputFluids: z.array(ResearchUnlockFluidSchema),
}).strict();
export const ResearchNodeDataSchema = z.object({
  guid: GuidSchema,
  state: ResearchNodeStateSchema,
  iconItemId: z.number(),
  position: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(GuidSchema),
  consumeItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  rewardItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  unlockItemRecipeViewItemIds: z.array(z.number()),
  unlockBlocks: z.array(z.object({ blockId: z.number(), blockGuid: GuidSchema }).strict()),
  unlockMachineRecipes: z.array(ResearchUnlockMachineRecipeSchema),
  unlockConnectToolGuids: z.array(GuidSchema),
  unlockTrainCarGuids: z.array(GuidSchema),
}).strict();
export const ResearchTreeDataSchema = z.object({ nodes: z.array(ResearchNodeDataSchema) }).strict();
