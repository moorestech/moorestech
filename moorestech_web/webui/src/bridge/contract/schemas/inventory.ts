import { z } from "zod";
import { GuidSchema, SlotDataSchema } from "./common";

export const PlayerInventoryDataSchema = z.object({
  mainSlots: z.array(SlotDataSchema),
  grab: SlotDataSchema,
  // 装備枠は可変長、-1は素手
  // Equipment is variable-length; -1 means bare hands
  equipment: z.array(SlotDataSchema),
  selectedEquipment: z.number(),
  equipmentSelectionConfirmationRevision: z.number().int().nonnegative(),
});

export const FluidSlotDataSchema = z.object({
  fluidId: z.number(),
  amount: z.number(),
  capacity: z.number(),
  // 表示名はguid導出キーで辞書解決する。空流体だけが空文字
  // The display name resolves from the guid-derived key; only the empty fluid carries an empty string
  fluidGuid: GuidSchema.or(z.literal("")),
}).strict();

export const MachineProcessStateSchema = z.enum(["idle", "processing", "halted"]);
export const MachineDetailDataSchema = z.object({
  recipeGuid: GuidSchema,
  selectedRecipeGuid: GuidSchema,
  blockGuid: GuidSchema,
  recipeTime: z.number(),
  outputItems: z.array(z.object({ itemId: z.number(), count: z.number() })),
  currentState: MachineProcessStateSchema,
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
  blockGuid: GuidSchema,
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
}).strict();
export const TrainInventoryOpenSchema = z.object({
  open: z.literal(true),
  source: z.literal("train"),
  blockType: z.literal("Train"),
  identifier: z.string(),
  itemSlots: z.array(SlotDataSchema),
  fluidSlots: z.array(FluidSlotDataSchema),
  error: z.enum(["containerMissing", "trainCarMissing", "openFailed"]).optional(),
});
export const BlockInventoryClosedSchema = z.object({ open: z.literal(false) });
export const BlockInventoryDataSchema = z.union([BlockInventoryOpenSchema, TrainInventoryOpenSchema, BlockInventoryClosedSchema]);
