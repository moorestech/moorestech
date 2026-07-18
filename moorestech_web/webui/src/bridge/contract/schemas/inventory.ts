import { z } from "zod";
import { SlotDataSchema } from "./common";

export const PlayerInventoryDataSchema = z.object({
  mainSlots: z.array(SlotDataSchema),
  hotbarSlots: z.array(SlotDataSchema),
  grab: SlotDataSchema,
  selectedHotbar: z.number(),
});

export const FluidSlotDataSchema = z.object({
  fluidId: z.number(),
  amount: z.number(),
  capacity: z.number(),
  name: z.string(),
});

export const MachineDetailDataSchema = z.object({
  recipeGuid: z.string(),
  recipeTime: z.number(),
  outputItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  currentState: z.string(),
  currentPower: z.number(),
  requestPower: z.number(),
  slotLayout: z.object({ input: z.number(), output: z.number(), module: z.number() }),
});

export const GeneratorDetailDataSchema = z.object({
  remainingFuelTime: z.number(), currentFuelTime: z.number(), operatingRate: z.number(),
});

export const MinerDetailDataSchema = z.object({
  currentPower: z.number(),
  requestPower: z.number(),
  miningItems: z.array(z.object({ itemId: z.number(), itemsPerMinute: z.number() })),
});

export const GearDetailDataSchema = z.object({
  isClockwise: z.boolean(), currentRpm: z.number(), currentTorque: z.number(), baseRpm: z.number(), baseTorque: z.number(),
});

export const ElectricNetworkDataSchema = z.object({
  totalGeneratePower: z.number(), totalRequiredPower: z.number(), consumerCount: z.number(), powerRate: z.number(),
});

export const GearNetworkStopReasonSchema = z.enum(["none", "rocked", "overRequirePower"]);
export const GearNetworkDataSchema = z.object({
  totalRequiredGearPower: z.number(),
  totalGenerateGearPower: z.number(),
  stopReason: GearNetworkStopReasonSchema,
});

export const FilterSplitterModeSchema = z.enum(["default", "whitelist", "blacklist"]);
export const FilterSplitterDirectionDataSchema = z.object({
  mode: FilterSplitterModeSchema,
  filterItemIds: z.array(z.number()),
});
export const FilterSplitterDataSchema = z.object({
  directionCount: z.number(),
  filterSlotCountPerDirection: z.number(),
  directions: z.array(FilterSplitterDirectionDataSchema),
});

export const ElectricToGearOutputModeDataSchema = z.object({
  rpm: z.number(),
  torque: z.number(),
  requiredPower: z.number(),
});
export const ElectricToGearDataSchema = z.object({
  selectedIndex: z.number().int().nonnegative(),
  fulfillmentRate: z.number(),
  consumedElectricPower: z.number(),
  outputModes: z.array(ElectricToGearOutputModeDataSchema),
});

export const TrainPlatformModeSchema = z.enum(["loadToTrain", "unloadToPlatform"]);
export const TrainPlatformDataSchema = z.object({
  mode: TrainPlatformModeSchema,
  itemSlotCount: z.number().int().nonnegative().optional(),
  fluidCapacity: z.number().nonnegative().optional(),
});

export const BlockInventoryOpenSchema = z.object({
  open: z.literal(true),
  source: z.literal("block"),
  blockType: z.string(),
  identifier: z.string(),
  blockName: z.string(),
  itemSlots: z.array(SlotDataSchema),
  fluidSlots: z.array(FluidSlotDataSchema),
  progress: z.number().optional(),
  machine: MachineDetailDataSchema.optional(),
  generator: GeneratorDetailDataSchema.optional(),
  miner: MinerDetailDataSchema.optional(),
  gear: GearDetailDataSchema.optional(),
  electricNetwork: ElectricNetworkDataSchema.optional(),
  gearNetwork: GearNetworkDataSchema.optional(),
  filterSplitter: FilterSplitterDataSchema.optional(),
  electricToGear: ElectricToGearDataSchema.optional(),
  trainPlatform: TrainPlatformDataSchema.optional(),
});
export const TrainInventoryOpenSchema = z.object({
  open: z.literal(true),
  source: z.literal("train"),
  blockType: z.literal("Train"),
  identifier: z.string(),
  blockName: z.string(),
  itemSlots: z.array(SlotDataSchema),
  fluidSlots: z.array(FluidSlotDataSchema),
  error: z.enum(["containerMissing", "trainCarMissing", "openFailed"]).optional(),
});
export const BlockInventoryClosedSchema = z.object({ open: z.literal(false) });
export const BlockInventoryDataSchema = z.union([BlockInventoryOpenSchema, TrainInventoryOpenSchema, BlockInventoryClosedSchema]);
