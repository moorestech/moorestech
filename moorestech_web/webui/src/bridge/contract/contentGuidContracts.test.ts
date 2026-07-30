import { describe, expect, it } from "vitest";
import { Topics } from "../transport/protocol";
import { validateTopicPayload } from "./validators";

const machineRecipe = {
  recipeGuid: "50000000-0000-4000-8000-000000000001",
  blockGuid: "40000000-0000-4000-8000-000000000001",
  blockId: 12,
  time: 1,
  inputItems: [{ itemId: 1, count: 2 }],
  outputItems: [{ itemId: 2, count: 1 }],
};

describe("content Guid contracts", () => {
  it("block inventoryはblockGuidを必須にし、旧blockNameの同居を拒否する", () => {
    const payload = {
      open: true,
      source: "block",
      blockType: "ElectricMachine",
      identifier: "(0, 0, 0)",
      itemSlots: [],
      fluidSlots: [],
    };

    const guidPayload = { ...payload, blockGuid: "40000000-0000-4000-8000-000000000001" };
    expect(validateTopicPayload(Topics.blockInventory, guidPayload)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, { ...guidPayload, blockName: "炉" })).toBe(false);
  });

  it("machine recipeはblockGuidを保持し、旧blockNameの同居を拒否する", () => {
    expect(validateTopicPayload(Topics.machineRecipes, { recipes: [machineRecipe] })).toBe(true);
    expect(validateTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockId: undefined, blockItemId: 12 }],
    })).toBe(false);
    expect(validateTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockName: "炉" }],
    })).toBe(false);
  });
});
