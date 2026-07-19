import { z } from "zod";

export const ResearchNodeStateSchema = z.enum([
  "completed",
  "researchable",
  "unresearchableNotEnoughItem",
  "unresearchableNotEnoughPreNode",
  "unresearchableAllReasons",
]);
export const ResearchNodeDataSchema = z.object({
  guid: z.string(),
  name: z.string(),
  description: z.string(),
  state: ResearchNodeStateSchema,
  iconItemId: z.number(),
  position: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(z.string()),
  consumeItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  rewardItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  unlockItemIds: z.array(z.number()),
});
export const ResearchTreeDataSchema = z.object({ nodes: z.array(ResearchNodeDataSchema) });
