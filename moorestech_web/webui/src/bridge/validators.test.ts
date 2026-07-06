import { describe, expect, it } from "vitest";
import { validateTopicPayload } from "./validators";
import { Topics } from "./protocol";

const openBase = {
  open: true, blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockName: "電気機械",
  itemSlots: [{ itemId: 1, count: 2 }], fluidSlots: [],
};

describe("validBlockInventory capability details", () => {
  it("accepts machine + electricNetwork details", () => {
    const d = {
      ...openBase,
      progress: 0.5,
      machine: { recipeGuid: "g", currentState: "processing", currentPower: 10, requestPower: 20, slotLayout: { input: 2, output: 1, module: 1 } },
      electricNetwork: { totalGeneratePower: 100, totalRequiredPower: 50, consumerCount: 3, powerRate: 1 },
    };
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
  });
  it("accepts gear + gearNetwork + generator + miner + filterSplitter details", () => {
    const d = {
      ...openBase,
      generator: { remainingFuelTime: 3, currentFuelTime: 10, operatingRate: 0.5 },
      miner: { currentPower: 1, requestPower: 2, miningItems: [{ itemId: 5, itemsPerMinute: 12 }] },
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5 },
      gearNetwork: { totalRequiredGearPower: 5, totalGenerateGearPower: 10, stopReason: "none" },
      filterSplitter: { directionCount: 2, filterSlotCountPerDirection: 3, directions: [{ mode: "whitelist", filterItemIds: [1, 0, 0] }, { mode: "default", filterItemIds: [0, 0, 0] }] },
    };
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
  });
  it("rejects malformed details", () => {
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, machine: { recipeGuid: 1 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, gearNetwork: { totalRequiredGearPower: 1, totalGenerateGearPower: 2, stopReason: 3 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, filterSplitter: { directionCount: 1, filterSlotCountPerDirection: 1, directions: [{ mode: "whitelist" }] } })).toBe(false);
  });
  it("still accepts details-less open and closed payloads", () => {
    expect(validateTopicPayload(Topics.blockInventory, openBase)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, { open: false })).toBe(true);
  });
});
