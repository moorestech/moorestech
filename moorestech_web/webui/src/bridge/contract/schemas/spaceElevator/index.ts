import { z } from "zod";

export const SpaceElevatorStatusSchema = z.enum(["readyToConnect", "insufficientCable", "waitingSatellite", "climbing", "lowPower", "outputBlocked"]);
export const SpaceElevatorDataSchema = z.object({
  connected: z.boolean(),
  cablePoolAmount: z.number().int().nonnegative(),
  cablesPerElevator: z.number().int().positive(),
  climbProgress: z.number().min(0).max(1),
  status: SpaceElevatorStatusSchema,
}).strict();
