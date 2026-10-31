import { z } from "zod";
import { InputSlotBindingSchema, refineBindingsCoverInputSlots } from "../common";

export const SpaceElevatorStatusSchema = z.enum(["readyToConnect", "insufficientCable", "waitingSatellite", "climbing", "lowPower", "outputBlocked", "inletBlocked"]);
export const SpaceElevatorDataSchema = z.object({
  satelliteInputSlots: z.array(z.number().int().nonnegative()),
  outputSlots: z.array(z.number().int().nonnegative()),
  connected: z.boolean(),
  cablePoolAmount: z.number().int().nonnegative(),
  cablesPerElevator: z.number().int().positive(),
  climbProgress: z.number().min(0).max(1),
  status: SpaceElevatorStatusSchema,
  // 衛星の入口の受け付けアイテム
  // Items accepted by the satellite inlet; Shift allocation narrows its destinations with these
  slotBindings: z.array(InputSlotBindingSchema),
}).strict().superRefine((data, ctx) => refineBindingsCoverInputSlots(data.slotBindings, data.satelliteInputSlots, ctx));
