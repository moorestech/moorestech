import type { BlockInventoryWireData } from "../../types/blockInventoryWireData";
import * as BlockGuids from "../blockLocalizationFixtures";

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

// 歯車発電機: 発電側（消費要求なし）
// Gear generator: the generating side (no consumption)
export const blockGearGenerator = {
  open: true,
  source: "block",
  blockType: "SimpleGearGenerator",
  identifier: "block:16",
  blockGuid: BlockGuids.GEAR_GENERATOR_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  gear: { currentRpm: 20.0, currentTorque: 5.0, role: "generator" },
  gearNetwork: { totalRequiredGearPower: 60.0, totalGenerateGearPower: 100.0, stopReason: "none" },
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

// 宇宙エレベーター拠点（未接続・接続可能）
// Space elevator base: unconnected and ready to connect
export const blockSpaceElevator = {
  open: true,
  source: "block",
  blockType: "SpaceElevatorBase",
  identifier: "block:20",
  blockGuid: BlockGuids.SPACE_ELEVATOR_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  spaceElevator: { connected: false, cablePoolAmount: 1000, cablesPerElevator: 1000, climbProgress: 0, status: "readyToConnect" },
} satisfies BlockInventoryWireData;

// 不足・昇降・出口待ちの受入状態
// Acceptance states for shortage, climbing, and blocked outlets
export const blockSpaceElevatorInsufficient = {
  ...blockSpaceElevator,
  spaceElevator: { ...blockSpaceElevator.spaceElevator, cablePoolAmount: 250, status: "insufficientCable" },
} satisfies BlockInventoryWireData;
export const blockSpaceElevatorClimbing = {
  ...blockSpaceElevator,
  spaceElevator: { connected: true, cablePoolAmount: 250, cablesPerElevator: 1000, climbProgress: 0.5, status: "climbing" },
} satisfies BlockInventoryWireData;
export const blockSpaceElevatorOutputBlocked = {
  ...blockSpaceElevatorClimbing,
  spaceElevator: { ...blockSpaceElevatorClimbing.spaceElevator, climbProgress: 1, status: "outputBlocked" },
} satisfies BlockInventoryWireData;
