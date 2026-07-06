import type { BlockInventoryData } from "../../src/bridge/contract/payloadTypes";

const empty = () => ({ itemId: 0, count: 0 });

// BLK-2 電気機械: 入出力+モジュールスロットと machine/electricNetwork capability
// BLK-2 electric machine: in/out+module slots with machine/electricNetwork capabilities
export const blockMachine = {
  open: true,
  blockType: "ElectricMachine",
  identifier: "block:3",
  blockName: "電気機械",
  itemSlots: [{ itemId: 3, count: 5 }, empty(), { itemId: 7, count: 1 }, empty()],
  fluidSlots: [{ fluidId: 1, amount: 25.5, capacity: 100.0, name: "水" }],
  progress: 0.42,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    currentState: "processing",
    currentPower: 80.0,
    requestPower: 100.0,
    slotLayout: { input: 2, output: 1, module: 1 },
  },
  electricNetwork: { totalGeneratePower: 500.0, totalRequiredPower: 300.0, consumerCount: 4, powerRate: 1.0 },
} satisfies BlockInventoryData;

// BLK-3 ギア機械: machine + gear/gearNetwork capability
// BLK-3 gear machine: machine + gear/gearNetwork capabilities
export const blockGearMachine = {
  open: true,
  blockType: "GearMachine",
  identifier: "block:4",
  blockName: "ギア機械",
  itemSlots: [{ itemId: 3, count: 2 }, empty()],
  fluidSlots: [],
  progress: 0.1,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    currentState: "idle",
    currentPower: 0.0,
    requestPower: 0.0,
    slotLayout: { input: 1, output: 1, module: 0 },
  },
  gear: { isClockwise: true, currentRpm: 12.5, currentTorque: 3.0, baseRpm: 20.0, baseTorque: 5.0 },
  gearNetwork: { totalRequiredGearPower: 60.0, totalGenerateGearPower: 100.0, stopReason: "none" },
} satisfies BlockInventoryData;

// BLK-4 発電機: 燃料スロットと generator/electricNetwork capability
// BLK-4 generator: fuel slot with generator/electricNetwork capabilities
export const blockGenerator = {
  open: true,
  blockType: "ElectricGenerator",
  identifier: "block:5",
  blockName: "発電機",
  itemSlots: [{ itemId: 9, count: 30 }],
  fluidSlots: [],
  generator: { remainingFuelTime: 12.5, currentFuelTime: 30.0, operatingRate: 0.75 },
  electricNetwork: { totalGeneratePower: 200.0, totalRequiredPower: 150.0, consumerCount: 2, powerRate: 1.0 },
} satisfies BlockInventoryData;

// BLK-5 採掘機: miner/electricNetwork capability と採掘アイテム毎分
// BLK-5 miner: miner/electricNetwork capabilities with mined items per minute
export const blockMiner = {
  open: true,
  blockType: "ElectricMiner",
  identifier: "block:6",
  blockName: "電動採掘機",
  itemSlots: [{ itemId: 11, count: 42 }, empty()],
  fluidSlots: [],
  progress: 0.66,
  miner: { currentPower: 50.0, requestPower: 100.0, miningItems: [{ itemId: 11, itemsPerMinute: 12.0 }] },
  electricNetwork: { totalGeneratePower: 100.0, totalRequiredPower: 100.0, consumerCount: 1, powerRate: 1.0 },
} satisfies BlockInventoryData;

// BLK-8 フィルタ分岐器: 3方向×2フィルタスロットの filterSplitter capability
// BLK-8 filter splitter: filterSplitter capability with 3 directions x 2 filter slots
export const blockFilterSplitter = {
  open: true,
  blockType: "FilterSplitter",
  identifier: "block:7",
  blockName: "フィルタ分岐器",
  itemSlots: [],
  fluidSlots: [],
  filterSplitter: {
    directionCount: 3,
    filterSlotCountPerDirection: 2,
    directions: [
      { mode: "whitelist", filterItemIds: [4, 0] },
      { mode: "default", filterItemIds: [0, 0] },
      { mode: "blacklist", filterItemIds: [7, 8] },
    ],
  },
} satisfies BlockInventoryData;
