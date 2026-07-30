import { describe, expect, it } from "vitest";
import { Topics } from "../transport/protocol";
import { validateTopicPayload } from "./validators";

const machineRecipe = {
  recipeGuid: "recipe-guid",
  blockGuid: "block-guid",
  blockId: 12,
  time: 1,
  inputItems: [{ itemId: 1, count: 2 }],
  outputItems: [{ itemId: 2, count: 1 }],
};

describe("content Guid contracts", () => {
  it("block inventoryはblockNameでなくblockGuidを必須にする", () => {
    const payload = {
      open: true,
      source: "block",
      blockType: "ElectricMachine",
      identifier: "(0, 0, 0)",
      itemSlots: [],
      fluidSlots: [],
    };

    expect(validateTopicPayload(Topics.blockInventory, { ...payload, blockGuid: "block-guid" })).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, { ...payload, blockName: "炉" })).toBe(false);
  });

  it("machine recipeはblockGuidを保持し、削除済みの名前では代替できない", () => {
    expect(validateTopicPayload(Topics.machineRecipes, { recipes: [machineRecipe] })).toBe(true);
    expect(validateTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockId: undefined, blockItemId: 12 }],
    })).toBe(false);
    expect(validateTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockGuid: undefined, blockName: "炉" }],
    })).toBe(false);
  });
});
