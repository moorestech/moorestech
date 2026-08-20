import { describe, expect, it } from "vitest";
import {
  ChallengeCurrentDataSchema,
  ChallengeTreeDataSchema,
  ResearchTreeDataSchema,
} from "./index";

const researchNode = {
  guid: "60000000-0000-4000-8000-000000000001",
  state: "researchable",
  iconItemId: 1,
  position: { x: 10, y: 20 },
  prevGuids: [],
  consumeItems: [],
  rewardItems: [],
  unlockItemRecipeViewItemIds: [],
  unlockBlocks: [],
  unlockMachineRecipes: [],
  unlockConnectToolGuids: [],
  unlockTrainCarGuids: [],
};

const challengeNode = {
  guid: "70000000-0000-4000-8000-000000000001",
  iconItemId: 1,
  state: "current",
  position: { x: 10, y: 20 },
  scale: { x: 1, y: 1 },
  prevGuids: [],
};

describe("research/challenge content identity contracts", () => {
  it("accepts Guid-only research and challenge payloads", () => {
    expect(ResearchTreeDataSchema.safeParse({ nodes: [researchNode] }).success).toBe(true);
    expect(ChallengeTreeDataSchema.safeParse({
      categories: [{ guid: "71000000-0000-4000-8000-000000000001", iconItemId: 1, nodes: [challengeNode] }],
    }).success).toBe(true);
    expect(ChallengeCurrentDataSchema.safeParse({
      challenges: [{ guid: "70000000-0000-4000-8000-000000000001", categoryGuid: "71000000-0000-4000-8000-000000000001" }],
    }).success).toBe(true);
  });

  it.each([
    { schema: ResearchTreeDataSchema, payload: { nodes: [{ ...researchNode, name: "legacy" }] } },
    { schema: ResearchTreeDataSchema, payload: { nodes: [{ ...researchNode, description: "legacy" }] } },
    {
      schema: ChallengeTreeDataSchema,
      payload: { categories: [{ guid: "71000000-0000-4000-8000-000000000001", name: "legacy", iconItemId: 1, nodes: [challengeNode] }] },
    },
    {
      schema: ChallengeTreeDataSchema,
      payload: { categories: [{ guid: "71000000-0000-4000-8000-000000000001", iconItemId: 1, nodes: [{ ...challengeNode, title: "legacy" }] }] },
    },
    {
      schema: ChallengeTreeDataSchema,
      payload: { categories: [{ guid: "71000000-0000-4000-8000-000000000001", iconItemId: 1, nodes: [{ ...challengeNode, summary: "legacy" }] }] },
    },
    {
      schema: ChallengeCurrentDataSchema,
      payload: { challenges: [{ guid: "70000000-0000-4000-8000-000000000001", categoryGuid: "71000000-0000-4000-8000-000000000001", title: "legacy" }] },
    },
  ])("rejects a removed display-text field", ({ schema, payload }) => {
    expect(schema.safeParse(payload).success).toBe(false);
  });

  it.each([
    {
      schema: ResearchTreeDataSchema,
      payload: { nodes: [researchNode], legacyDisplayText: "research" },
    },
    {
      schema: ChallengeTreeDataSchema,
      payload: {
        categories: [{ guid: "71000000-0000-4000-8000-000000000001", iconItemId: 1, nodes: [challengeNode] }],
        legacyDisplayText: "challenge tree",
      },
    },
    {
      schema: ChallengeCurrentDataSchema,
      payload: {
        challenges: [{ guid: "70000000-0000-4000-8000-000000000001", categoryGuid: "71000000-0000-4000-8000-000000000001" }],
        legacyDisplayText: "challenge current",
      },
    },
  ])("rejects an unknown top-level field beside a valid payload", ({ schema, payload }) => {
    expect(schema.safeParse(payload).success).toBe(false);
  });
});
