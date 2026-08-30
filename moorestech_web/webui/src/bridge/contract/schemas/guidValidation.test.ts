import { describe, expect, it } from "vitest";
import {
  BlockInventoryOpenSchema,
  ChallengeCategoryDataSchema,
  ChallengeCurrentDataSchema,
  ChallengeNodeDataSchema,
  CraftRecipeSchema,
  CurrentChallengeDataSchema,
  ItemMasterEntrySchema,
  MachineDetailDataSchema,
  MachineRecipeSchema,
  ResearchNodeDataSchema,
} from "./index";

const invalidGuid = "symbolic-guid";
const guidA = "10000000-0000-4000-8000-000000000001";
const guidB = "20000000-0000-4000-8000-000000000002";
const guidC = "30000000-0000-4000-8000-000000000003";

const machineDetail = {
  recipeGuid: guidA,
  selectedRecipeGuid: guidB,
  blockGuid: guidC,
  recipeTime: 1,
  outputItems: [],
  currentState: "processing",
  currentPower: 10,
  requestPower: 20,
  slotLayout: { input: 1, output: 1, module: 0 },
};

const blockInventory = {
  open: true,
  source: "block",
  blockType: "ElectricMachine",
  identifier: "(0, 0, 0)",
  blockGuid: guidC,
  itemSlots: [],
  fluidSlots: [],
};

const craftRecipe = {
  recipeGuid: guidA,
  resultItemId: 1,
  resultCount: 1,
  craftTime: 1,
  requiredItems: [],
};

const machineRecipe = {
  recipeGuid: guidA,
  blockGuid: guidC,
  blockId: 1,
  time: 1,
  inputItems: [],
  outputItems: [],
  inputFluids: [],
  outputFluids: [],
};

const researchNode = {
  guid: guidA,
  state: "researchable",
  iconItemId: 1,
  position: { x: 0, y: 0 },
  prevGuids: [guidB],
  consumeItems: [],
  rewardItems: [],
  unlockItemRecipeViewItemIds: [],
  unlockBlocks: [{ blockId: 1, blockGuid: guidC }],
  unlockMachineRecipes: [{ recipeGuid: guidA, outputItemIds: [], outputFluids: [{ fluidId: 1, amount: 1, fluidGuid: guidB }] }],
  unlockConnectToolGuids: [guidB],
  unlockTrainCarGuids: [guidC],
};

const challengeNode = {
  guid: guidA,
  iconItemId: 1,
  state: "current",
  position: { x: 0, y: 0 },
  scale: { x: 1, y: 1 },
  prevGuids: [guidB],
};

describe("content GUID schemas", () => {
  it.each([
    { label: "machine recipeGuid", schema: MachineDetailDataSchema, payload: { ...machineDetail, recipeGuid: invalidGuid } },
    { label: "machine selectedRecipeGuid", schema: MachineDetailDataSchema, payload: { ...machineDetail, selectedRecipeGuid: invalidGuid } },
    { label: "machine blockGuid", schema: MachineDetailDataSchema, payload: { ...machineDetail, blockGuid: invalidGuid } },
    { label: "block inventory blockGuid", schema: BlockInventoryOpenSchema, payload: { ...blockInventory, blockGuid: invalidGuid } },
    { label: "craft recipeGuid", schema: CraftRecipeSchema, payload: { ...craftRecipe, recipeGuid: invalidGuid } },
    { label: "machine recipe recipeGuid", schema: MachineRecipeSchema, payload: { ...machineRecipe, recipeGuid: invalidGuid } },
    { label: "machine recipe blockGuid", schema: MachineRecipeSchema, payload: { ...machineRecipe, blockGuid: invalidGuid } },
    { label: "machine recipe inputFluids fluidGuid", schema: MachineRecipeSchema, payload: { ...machineRecipe, inputFluids: [{ fluidId: 1, amount: 1, fluidGuid: invalidGuid }] } },
    { label: "item master itemGuid", schema: ItemMasterEntrySchema, payload: { itemId: 1, itemGuid: invalidGuid, maxStack: 100 } },
    { label: "research guid", schema: ResearchNodeDataSchema, payload: { ...researchNode, guid: invalidGuid } },
    { label: "research prevGuids", schema: ResearchNodeDataSchema, payload: { ...researchNode, prevGuids: [invalidGuid] } },
    {
      label: "research unlockBlocks blockGuid",
      schema: ResearchNodeDataSchema,
      payload: { ...researchNode, unlockBlocks: [{ blockId: 1, blockGuid: invalidGuid }] },
    },
    {
      label: "research unlockMachineRecipes recipeGuid",
      schema: ResearchNodeDataSchema,
      payload: { ...researchNode, unlockMachineRecipes: [{ recipeGuid: invalidGuid, outputItemIds: [], outputFluids: [] }] },
    },
    {
      label: "research unlockMachineRecipes outputFluids fluidGuid",
      schema: ResearchNodeDataSchema,
      payload: { ...researchNode, unlockMachineRecipes: [{ recipeGuid: guidA, outputItemIds: [], outputFluids: [{ fluidId: 1, amount: 1, fluidGuid: invalidGuid }] }] },
    },
    {
      label: "research unlockConnectToolGuids",
      schema: ResearchNodeDataSchema,
      payload: { ...researchNode, unlockConnectToolGuids: [invalidGuid] },
    },
    {
      label: "research unlockTrainCarGuids",
      schema: ResearchNodeDataSchema,
      payload: { ...researchNode, unlockTrainCarGuids: [invalidGuid] },
    },
    { label: "challenge guid", schema: ChallengeNodeDataSchema, payload: { ...challengeNode, guid: invalidGuid } },
    { label: "challenge prevGuids", schema: ChallengeNodeDataSchema, payload: { ...challengeNode, prevGuids: [invalidGuid] } },
    {
      label: "challenge category guid",
      schema: ChallengeCategoryDataSchema,
      payload: { guid: invalidGuid, iconItemId: 1, nodes: [challengeNode] },
    },
    {
      label: "current challenge guid",
      schema: CurrentChallengeDataSchema,
      payload: { guid: invalidGuid, categoryGuid: guidB },
    },
    {
      label: "current challenge categoryGuid",
      schema: CurrentChallengeDataSchema,
      payload: { guid: guidA, categoryGuid: invalidGuid },
    },
    {
      label: "completed challenge guid",
      schema: ChallengeCurrentDataSchema,
      payload: { challenges: [], completedChallengeGuid: invalidGuid },
    },
  ])("rejects an invalid $label", ({ schema, payload }) => {
    expect(schema.safeParse(payload).success).toBe(false);
  });
});
