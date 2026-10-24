import { describe, expect, it } from "vitest";
import { parseTopicPayload } from "../validators";
import { Topics } from "../../transport/protocol";


const openBase = {
  open: true, source: "block", blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockGuid: "40000000-0000-4000-8000-000000000001",
  itemSlots: [{ itemId: 1, count: 2 }], fluidSlots: [],
};

describe("validBlockInventory capability details", () => {
  it("accepts machine + electricNetwork details", () => {
    const d = {
      ...openBase,
      progress: 0.5,
      machine: { recipeGuid: "50000000-0000-4000-8000-000000000001", selectedRecipeGuid: "50000000-0000-4000-8000-000000000002", blockGuid: "40000000-0000-4000-8000-000000000001", recipeTime: 15, outputItems: [{ itemId: 2, count: 3 }], currentState: "processing", currentPower: 10, requestPower: 20, slotLayout: { input: 2, output: 1, module: 1, inputTank: 0 }, slotBindings: [{ slot: 0, itemId: 1, count: 2, acceptedItemIds: [1] }], tankBindings: [] },
      electricNetwork: { totalGeneratePower: 100, totalRequiredPower: 50, consumerCount: 3, powerRate: 1 },
    };
    expect(parseTopicPayload(Topics.blockInventory, d).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...d,
      machine: { ...d.machine, selectedRecipeGuid: undefined, blockGuid: undefined },
    }).valid).toBe(false);
  });
  it("accepts gear + gearNetwork + generator + miner + filterSplitter + electricToGear details", () => {
    const d = {
      ...openBase,
      blockType: "ElectricToGearGenerator",
      generator: { remainingFuelTime: 3, currentFuelTime: 10, operatingRate: 0.5 },
      miner: { currentPower: 1, requestPower: 2, miningItems: [{ itemId: 5, itemsPerMinute: 12 }] },
      gear: { currentRpm: 10, currentTorque: 3, baseRpm: 20, role: "consumer" },
      gearNetwork: { totalRequiredGearPower: 5, totalGenerateGearPower: 10, stopReason: "none" },
      filterSplitter: { directionCount: 2, filterSlotCountPerDirection: 3, directions: [{ mode: "whitelist", filterItemIds: [1, 0, 0] }, { mode: "default", filterItemIds: [0, 0, 0] }] },
      electricToGear: {
        selectedIndex: 1,
        fulfillmentRate: 0.75,
        consumedElectricPower: 10,
        outputModes: [{ rpm: 10, torque: 10, requiredPower: 10 }, { rpm: 20, torque: 20, requiredPower: 10 }],
      },
    };
    expect(parseTopicPayload(Topics.blockInventory, d).valid).toBe(true);
    // 旧形状（role無し・baseTorque入り）は判別子の欠落と余剰キーの両方で落ちる
    // The old shape (no role, with baseTorque) fails both on the missing discriminator and on the excess key
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5 },
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5, role: "consumer" },
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { currentRpm: 10, currentTorque: 3, role: "generator" },
    }).valid).toBe(true);
    // 発電機は基準RPMを持たず、消費側は必ず持つ
    // Generators carry no base RPM and consumers always do
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { currentRpm: 10, currentTorque: 3, baseRpm: 0, role: "generator" },
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { currentRpm: 10, currentTorque: 3, role: "consumer" },
    }).valid).toBe(false);
  });

  it("rejects electricToGear without output mode power", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      blockType: "ElectricToGearGenerator",
      electricToGear: {
        selectedIndex: 0,
        fulfillmentRate: 1,
        consumedElectricPower: 10,
        outputModes: [{ rpm: 10, torque: 10 }],
      },
    }).valid).toBe(false);
  });
  it("accepts a gearToElectric detail", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      blockType: "GearToElectricGenerator",
      gearToElectric: { generationState: "idle", suppliedPower: 0, maxGeneratedPower: 100 },
    }).valid).toBe(true);
  });
  it("rejects an unknown gearToElectric generation state", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      blockType: "GearToElectricGenerator",
      gearToElectric: { generationState: "charging", suppliedPower: 0, maxGeneratedPower: 100 },
    }).valid).toBe(false);
  });
  it("rejects malformed details", () => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, machine: { recipeGuid: 1 } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, gearNetwork: { totalRequiredGearPower: 1, totalGenerateGearPower: 2, stopReason: 3 } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, filterSplitter: { directionCount: 1, filterSlotCountPerDirection: 1, directions: [{ mode: "whitelist" }] } }).valid).toBe(false);
  });
  it("still accepts details-less open and closed payloads", () => {
    expect(parseTopicPayload(Topics.blockInventory, openBase).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, { open: false }).valid).toBe(true);
  });
});


// 種別と詳細の組合せを受信口で検査する
// Verify converter kind/detail combinations through the receive entry point
describe("converter detail ownership", () => {
  const gearToElectric = { generationState: "idle", suppliedPower: 0, maxGeneratedPower: 100 };
  const electricToGear = { selectedIndex: 0, fulfillmentRate: 1, consumedElectricPower: 10, outputModes: [{ rpm: 10, torque: 10, requiredPower: 10 }] };
  it.each(["Chest", "ElectricToGearGenerator"])("rejects gearToElectric on %s", (blockType) => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, blockType, gearToElectric }).valid).toBe(false);
  });
  it.each(["Chest", "GearToElectricGenerator"])("rejects electricToGear on %s", (blockType) => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, blockType, electricToGear }).valid).toBe(false);
  });
  it.each(["GearToElectricGenerator", "ElectricToGearGenerator"])("rejects both details on %s", (blockType) => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, blockType, gearToElectric, electricToGear }).valid).toBe(false);
  });
  it.each(["GearToElectricGenerator", "ElectricToGearGenerator"])("allows pending detail on %s", (blockType) => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, blockType }).valid).toBe(true);
  });
});

describe("validBlockInventory detail section parents", () => {
  const cleanRoom = { fetchState: "fetched", kind: "noEnclosure" };
  const cleanRoomYield = { fetchState: "fetched", role: "dicing", dieLossRate: 0.03 };
  const machineInputStall = { fetchState: "notFetched" };

  // 3欄は欄ごとに許された親ブロックの種別にだけ載り、Chest 等への混入は境界で拒否する
  // Each of the three sections rides only on its allowed parent block types; mixing into a Chest etc. is rejected at the boundary
  it.each([
    ["CleanRoomMachine", { cleanRoom, cleanRoomYield, machineInputStall }, true],
    ["CleanRoomAirFilter", { cleanRoom }, true],
    ["ElectricMachine", { machineInputStall }, true],
    ["GearMachine", { machineInputStall }, true],
    ["Chest", { cleanRoom }, false],
    ["Chest", { cleanRoomYield }, false],
    ["Chest", { machineInputStall }, false],
    ["CleanRoomAirFilter", { cleanRoomYield }, false],
    ["CleanRoomAirFilter", { machineInputStall }, false],
    ["ElectricMachine", { cleanRoom }, false],
    ["GearMachine", { cleanRoomYield }, false],
    ["Chest", {}, true],
  ])("%s with %j の受理は %s", (blockType, sections, expected) => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, blockType, ...sections }).valid).toBe(expected);
  });
});
