import type { BlockInventoryWireData } from "../../src/bridge/contract/payloadTypes";
import * as BlockGuids from "./fixtures/blockLocalizationFixtures";
import { WATER_FLUID_GUID } from "./fixtures/contentLocalizationFixtures";

const empty = () => ({ itemId: 0, count: 0 });

// BLK-2 電気機械: 入出力+モジュールスロットと machine/electricNetwork capability
// BLK-2 electric machine: in/out+module slots with machine/electricNetwork capabilities
export const blockMachine = {
  open: true,
  source: "block",
  blockType: "ElectricMachine",
  identifier: "block:3",
  blockGuid: BlockGuids.ELECTRIC_MACHINE_BLOCK_GUID,
  // 選択中(bbbbbbbb)の素材itemId2をスロット0に置き、出力/液体スロットは空にしてゴースト描画を検証する
  // Places selected recipe (bbbbbbbb)'s material itemId 2 in slot 0 and leaves the output/fluid slots empty to exercise ghost rendering
  itemSlots: [{ itemId: 2, count: 5 }, empty(), empty(), empty()],
  fluidSlots: [{ fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }],
  progress: 0.42,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    selectedRecipeGuid: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
    blockGuid: BlockGuids.ELECTRIC_MACHINE_BLOCK_GUID,
    recipeTime: 15,
    outputItems: [{ itemId: 7, count: 3 }],
    currentState: "processing",
    currentPower: 80.0,
    requestPower: 100.0,
    slotLayout: { input: 2, output: 1, module: 1, inputTank: 1 },
    // 選択中(bbbbbbbb)のレシピ束縛。ホストが配信する正本と同じ形をmockでも持たせる
    // Binding of the selected recipe (bbbbbbbb); the mock carries the same shape the host publishes
    slotBindings: [{ slot: 0, itemId: 2, count: 3 }, { slot: 2, itemId: 7, count: 2 }],
    tankBindings: [{ tank: 0, fluidGuid: WATER_FLUID_GUID, amount: 10 }],
  },
  electricNetwork: { totalGeneratePower: 500.0, totalRequiredPower: 300.0, consumerCount: 4, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-3 ギア機械: machine + gear/gearNetwork capability
// BLK-3 gear machine: machine + gear/gearNetwork capabilities
export const blockGearMachine = {
  open: true,
  source: "block",
  blockType: "GearMachine",
  identifier: "block:4",
  blockGuid: BlockGuids.GEAR_MACHINE_BLOCK_GUID,
  itemSlots: [{ itemId: 3, count: 2 }, empty()],
  fluidSlots: [],
  progress: 0.1,
  machine: {
    recipeGuid: "00000000-0000-0000-0000-000000000000",
    selectedRecipeGuid: "00000000-0000-0000-0000-000000000000",
    blockGuid: BlockGuids.GEAR_MACHINE_BLOCK_GUID,
    recipeTime: 15,
    outputItems: [{ itemId: 7, count: 3 }],
    currentState: "idle",
    currentPower: 0.0,
    requestPower: 0.0,
    slotLayout: { input: 1, output: 1, module: 0, inputTank: 0 },
    slotBindings: [],
    tankBindings: [],
  },
  gear: { isClockwise: true, currentRpm: 12.5, currentTorque: 3.0, baseRpm: 20.0, baseTorque: 5.0 },
  gearNetwork: { totalRequiredGearPower: 60.0, totalGenerateGearPower: 100.0, stopReason: "none" },
} satisfies BlockInventoryWireData;

// BLK-4 発電機: 燃料スロットと generator/electricNetwork capability
// BLK-4 generator: fuel slot with generator/electricNetwork capabilities
export const blockGenerator = {
  open: true,
  source: "block",
  blockType: "ElectricGenerator",
  identifier: "block:5",
  blockGuid: BlockGuids.GENERATOR_BLOCK_GUID,
  itemSlots: [{ itemId: 9, count: 30 }],
  fluidSlots: [],
  generator: { remainingFuelTime: 12.5, currentFuelTime: 30.0, operatingRate: 0.75 },
  electricNetwork: { totalGeneratePower: 200.0, totalRequiredPower: 150.0, consumerCount: 2, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-5 採掘機: miner/electricNetwork capability と採掘アイテム毎分
// BLK-5 miner: miner/electricNetwork capabilities with mined items per minute
export const blockMiner = {
  open: true,
  source: "block",
  blockType: "ElectricMiner",
  identifier: "block:6",
  blockGuid: BlockGuids.MINER_BLOCK_GUID,
  itemSlots: [{ itemId: 11, count: 42 }, empty()],
  fluidSlots: [],
  progress: 0.66,
  miner: { currentPower: 50.0, requestPower: 100.0, miningItems: [{ itemId: 11, itemsPerMinute: 12.0 }] },
  electricNetwork: { totalGeneratePower: 100.0, totalRequiredPower: 100.0, consumerCount: 1, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-6 ギア採掘機: miner/gear/gearNetwork の複合表示を検証する
// BLK-6 gear miner: exercises the combined miner/gear/gearNetwork stack
export const blockGearMiner = {
  open: true,
  source: "block",
  blockType: "GearMiner",
  identifier: "block:8",
  blockGuid: BlockGuids.GEAR_MINER_BLOCK_GUID,
  itemSlots: [{ itemId: 11, count: 8 }],
  fluidSlots: [],
  progress: 0.25,
  miner: { currentPower: 20.0, requestPower: 40.0, miningItems: [{ itemId: 11, itemsPerMinute: 6.0 }] },
  gear: { isClockwise: false, currentRpm: 8.0, currentTorque: 2.0, baseRpm: 12.0, baseTorque: 3.0 },
  gearNetwork: { totalRequiredGearPower: 24.0, totalGenerateGearPower: 40.0, stopReason: "none" },
} satisfies BlockInventoryWireData;

// BLK-7 未登録種別: generic fallback の item/fluid 表示を検証する
// BLK-7 unregistered type: exercises the generic item/fluid fallback
export const blockGeneric = {
  open: true,
  source: "block",
  blockType: "UnregisteredBlock",
  identifier: "block:9",
  blockGuid: BlockGuids.GENERIC_BLOCK_GUID,
  itemSlots: [{ itemId: 1, count: 1 }],
  fluidSlots: [{ fluidId: 1, amount: 10, capacity: 20, fluidGuid: WATER_FLUID_GUID }],
  progress: 0.5,
} satisfies BlockInventoryWireData;

// BLK-8 フィルタ分岐器: 3方向×2フィルタスロットの filterSplitter capability
// BLK-8 filter splitter: filterSplitter capability with 3 directions x 2 filter slots
export const blockFilterSplitter = {
  open: true,
  source: "block",
  blockType: "FilterSplitter",
  identifier: "block:7",
  blockGuid: BlockGuids.FILTER_SPLITTER_BLOCK_GUID,
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
} satisfies BlockInventoryWireData;

// 回転生成機の動的fixture
// B1 electric-to-gear converter: master-ordered modes and dynamic StateDetail values
export const blockElectricToGear = {
  open: true,
  source: "block",
  blockType: "ElectricToGearGenerator",
  identifier: "block:10",
  blockGuid: BlockGuids.ELECTRIC_TO_GEAR_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  electricToGear: {
    selectedIndex: 1,
    fulfillmentRate: 0.75,
    consumedElectricPower: 10,
    outputModes: [
      { rpm: 10, torque: 10, requiredPower: 10 },
      { rpm: 20, torque: 20, requiredPower: 10 },
    ],
  },
} satisfies BlockInventoryWireData;

// B2 列車PFのスロットとモード
// B2 train platform: item slots and transfer mode
export const blockTrainPlatform = {
  open: true,
  source: "block",
  blockType: "TrainItemPlatform",
  identifier: "block:11",
  blockGuid: BlockGuids.TRAIN_ITEM_PLATFORM_BLOCK_GUID,
  itemSlots: [{ itemId: 3, count: 12 }, empty()],
  fluidSlots: [],
  trainPlatform: { mode: "loadToTrain", itemSlotCount: 2 },
} satisfies BlockInventoryWireData;

// B2 液体PFのマスタ容量
// B2 fluid platform: the same master capacity shown by uGUI
export const blockTrainFluidPlatform = {
  open: true,
  source: "block",
  blockType: "TrainFluidPlatform",
  identifier: "block:12",
  blockGuid: BlockGuids.TRAIN_FLUID_PLATFORM_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  trainPlatform: { mode: "unloadToPlatform", fluidCapacity: 1000 },
} satisfies BlockInventoryWireData;

// B2 電柱の電力集約値
// B2 electric pole: electric-network aggregates sampled every second
export const blockElectricPole = {
  open: true,
  source: "block",
  blockType: "ElectricPole",
  identifier: "block:13",
  blockGuid: BlockGuids.ELECTRIC_POLE_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  electricNetwork: { totalGeneratePower: 240, totalRequiredPower: 180, consumerCount: 3, powerRate: 1 },
} satisfies BlockInventoryWireData;
