import { z } from "zod";

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

export const GearToElectricGenerationStateSchema = z.enum(["generating", "idle", "stopped"]);
export const GearToElectricDataSchema = z.object({
  generationState: GearToElectricGenerationStateSchema,
  suppliedPower: z.number(),
  maxGeneratedPower: z.number(),
}).strict();
