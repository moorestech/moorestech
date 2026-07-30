import { z } from "zod";

export const ChallengeNodeStateSchema = z.enum(["locked", "current", "completed"]);
export const ChallengeNodeDataSchema = z.object({
  guid: z.string(),
  iconItemId: z.number(),
  state: ChallengeNodeStateSchema,
  position: z.object({ x: z.number(), y: z.number() }),
  scale: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(z.string()),
}).strict();
export const ChallengeCategoryDataSchema = z.object({
  guid: z.string(),
  iconItemId: z.number(),
  nodes: z.array(ChallengeNodeDataSchema),
}).strict();
export const ChallengeTreeDataSchema = z.object({ categories: z.array(ChallengeCategoryDataSchema) }).strict();
export const CurrentChallengeDataSchema = z.object({
  guid: z.string(),
  categoryGuid: z.string(),
}).strict();
export const ChallengeCurrentDataSchema = z.object({
  challenges: z.array(CurrentChallengeDataSchema),
  completedChallengeGuid: z.string().optional(),
}).strict();
