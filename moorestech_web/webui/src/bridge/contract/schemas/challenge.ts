import { z } from "zod";

export const ChallengeNodeStateSchema = z.enum(["locked", "current", "completed"]);
export const ChallengeNodeDataSchema = z.object({
  guid: z.string(),
  title: z.string(),
  summary: z.string(),
  iconItemId: z.number(),
  state: ChallengeNodeStateSchema,
  position: z.object({ x: z.number(), y: z.number() }),
  scale: z.object({ x: z.number(), y: z.number() }),
  prevGuids: z.array(z.string()),
});
export const ChallengeCategoryDataSchema = z.object({
  guid: z.string(),
  name: z.string(),
  iconItemId: z.number(),
  nodes: z.array(ChallengeNodeDataSchema),
});
export const ChallengeTreeDataSchema = z.object({ categories: z.array(ChallengeCategoryDataSchema) });
export const CurrentChallengeDataSchema = z.object({
  guid: z.string(),
  title: z.string(),
  categoryGuid: z.string(),
});
export const ChallengeCurrentDataSchema = z.object({
  challenges: z.array(CurrentChallengeDataSchema),
  completedChallengeGuid: z.string().optional(),
});
