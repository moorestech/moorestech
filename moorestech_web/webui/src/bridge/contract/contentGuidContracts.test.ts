import { describe, expect, it } from "vitest";
import { Topics } from "../transport/protocol";
import { parseTopicPayload } from "./validators";

const machineRecipe = {
  recipeGuid: "50000000-0000-4000-8000-000000000001",
  blockGuid: "40000000-0000-4000-8000-000000000001",
  blockId: 12,
  time: 1,
  inputItems: [{ itemId: 1, count: 2 }],
  outputItems: [{ itemId: 2, count: 1 }],
  inputFluids: [],
  outputFluids: [],
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
    expect(parseTopicPayload(Topics.blockInventory, guidPayload).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, { ...guidPayload, blockName: "炉" }).valid).toBe(false);
  });

  it("fluid slotはfluidGuidを必須にし、旧nameの同居を拒否する", () => {
    const fluidSlot = { fluidId: 10, amount: 500, capacity: 1000 };
    const payload = {
      open: true,
      source: "block",
      blockType: "Tank",
      identifier: "(0, 0, 0)",
      blockGuid: "40000000-0000-4000-8000-000000000001",
      itemSlots: [],
    };

    expect(parseTopicPayload(Topics.blockInventory, {
      ...payload,
      fluidSlots: [{ ...fluidSlot, fluidGuid: "60000000-0000-4000-8000-000000000001" }],
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...payload,
      fluidSlots: [{ ...fluidSlot, fluidGuid: "60000000-0000-4000-8000-000000000001", name: "水" }],
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...payload,
      fluidSlots: [{ ...fluidSlot, name: "水" }],
    }).valid).toBe(false);

    // 空流体だけがGuid空文字を許される
    // Only the empty fluid may carry an empty GUID string
    expect(parseTopicPayload(Topics.blockInventory, {
      ...payload,
      fluidSlots: [{ fluidId: 0, amount: 0, capacity: 1000, fluidGuid: "" }],
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...payload,
      fluidSlots: [{ ...fluidSlot, fluidGuid: "not-a-guid" }],
    }).valid).toBe(false);
  });

  it("machine recipeはblockGuidを保持し、旧blockNameの同居を拒否する", () => {
    expect(parseTopicPayload(Topics.machineRecipes, { recipes: [machineRecipe] }).valid).toBe(true);
    expect(parseTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockId: undefined, blockItemId: 12 }],
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...machineRecipe, blockName: "炉" }],
    }).valid).toBe(false);
  });
});
