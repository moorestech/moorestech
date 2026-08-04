import { z } from "zod";
import { GuidSchema } from "./common";

export const ChallengeNodeStateSchema = z.enum(["locked", "current", "completed"]);
export const ChallengeNodeDataSchema = z.object({
  guid: GuidSchema,
  iconItemId: z.number(),
  state: ChallengeNodeStateSchema,
  position: z.object({ x: z.number(), y: z.number() }),
  scale: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(GuidSchema),
}).strict();
export const ChallengeCategoryDataSchema = z.object({
  guid: GuidSchema,
  iconItemId: z.number(),
  nodes: z.array(ChallengeNodeDataSchema),
}).strict();
export const ChallengeTreeDataSchema = z.object({ categories: z.array(ChallengeCategoryDataSchema) }).strict();
export const CurrentChallengeDataSchema = z.object({
  guid: GuidSchema,
  categoryGuid: GuidSchema,
}).strict();
export const ChallengeCurrentDataSchema = z.object({
  challenges: z.array(CurrentChallengeDataSchema),
  completedChallengeGuid: GuidSchema.optional(),
}).strict();
