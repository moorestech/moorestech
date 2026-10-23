import { z } from "zod";

// 発射台の段階・待ち理由・進み具合と、C# が並べたスロット群の絶対添字。C# の RocketLaunchPadDetailDtoBuilder と1対1
// Launch pad phase, wait reason, progress and the slot group indices laid out by C#; mirrors RocketLaunchPadDetailDtoBuilder one-to-one
const RocketLaunchPadPhaseSchema = z.enum(["idle", "awaitingBuildCommand", "building", "awaitingLaunchCommand", "readyToLaunch", "launching"]);
const RocketLaunchPadWaitReasonSchema = z.enum(["none", "missingMaterials", "missingPayload", "outputFull", "noPower"]);
const SlotIndexListSchema = z.array(z.number().int().nonnegative());
export const RocketLaunchPadDataSchema = z.object({
  phase: RocketLaunchPadPhaseSchema,
  waitReason: RocketLaunchPadWaitReasonSchema,
  progress: z.number().min(0).max(1),
  materialSlots: SlotIndexListSchema,
  payloadSlots: SlotIndexListSchema,
  outputSlots: SlotIndexListSchema,
}).strict();
