import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import * as BlockGuids from "../blockLocalizationFixtures";
import { WATER_FLUID_GUID } from "../contentLocalizationFixtures";

const empty = () => ({ itemId: 0, count: 0 });

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
  gear: { currentRpm: 8.0, currentTorque: 2.0, baseRpm: 12.0, role: "consumer" },
  gearNetwork: { totalRequiredGearPower: 24.0, totalGenerateGearPower: 40.0, stopReason: "none" },
} satisfies BlockInventoryWireData;

// BLK-10: 油井、汲み上げ中流体あり
// BLK-10: electric pump, active target
export const blockPump = {
  open: true,
  source: "block",
  blockType: "ElectricPump",
  identifier: "block:10",
  blockGuid: BlockGuids.ELECTRIC_PUMP_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [{ fluidId: 1, amount: 120, capacity: 200, fluidGuid: WATER_FLUID_GUID }],
  pump: {
    kind: "electric",
    electric: { currentState: "processing", currentPower: 50.0, requestPower: 50.0 },
    pumpingFluids: [{ fluidGuid: WATER_FLUID_GUID, amountPerMinute: 3600.0 }],
  },
  electricNetwork: { totalGeneratePower: 100.0, totalRequiredPower: 50.0, consumerCount: 1, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-11: 鉱脈外の油井、警告行あり
// BLK-11: pump off vein, shows warning
export const blockPumpNoVein = {
  ...blockPump,
  identifier: "block:11",
  fluidSlots: [{ fluidId: 0, amount: 0, capacity: 200, fluidGuid: "" }],
  pump: { kind: "electric", electric: { currentState: "idle", currentPower: 10.0, requestPower: 10.0 }, pumpingFluids: [] },
} satisfies BlockInventoryWireData;

// BLK-12: 歯車ポンプ
// BLK-12: gear pump
export const blockGearPump = {
  open: true,
  source: "block",
  blockType: "GearPump",
  identifier: "block:12",
  blockGuid: BlockGuids.GEAR_PUMP_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [{ fluidId: 1, amount: 30, capacity: 100, fluidGuid: WATER_FLUID_GUID }],
  pump: { kind: "gear", pumpingFluids: [{ fluidGuid: WATER_FLUID_GUID, amountPerMinute: 120.0 }] },
  gear: { currentRpm: 10.0, currentTorque: 2.0, baseRpm: 10.0, role: "consumer" },
  gearNetwork: { totalRequiredGearPower: 20.0, totalGenerateGearPower: 40.0, stopReason: "none" },
} satisfies BlockInventoryWireData;
