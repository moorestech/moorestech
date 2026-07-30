import { z } from "zod";
import { GuidSchema } from "./common";

export const ResearchNodeStateSchema = z.enum([
  "completed",
  "researchable",
  "unresearchableNotEnoughItem",
  "unresearchableNotEnoughPreNode",
  "unresearchableAllReasons",
]);
export const ResearchNodeDataSchema = z.object({
  guid: GuidSchema,
  state: ResearchNodeStateSchema,
  iconItemId: z.number(),
  position: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(GuidSchema),
  consumeItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  rewardItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  unlockItemIds: z.array(z.number()),
}).strict();
export const ResearchTreeDataSchema = z.object({ nodes: z.array(ResearchNodeDataSchema) }).strict();
