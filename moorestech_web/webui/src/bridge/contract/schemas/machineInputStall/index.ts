import { z } from "zod";

// 機械の搬入が継承元のレベルで止まっている理由と、届いて待っている品
// Why a machine's inflow is stalled on the inheritance source level, and the item that arrived and waits
export const MachineInputStallDataSchema = z.object({
  reason: z.enum(["mixedLevel", "aboveCraftCap"]),
  itemId: z.number().int().positive(),
}).strict();
