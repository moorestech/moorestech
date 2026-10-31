import { z } from "zod";
import { BlockInfoUnavailableSchema, blockInfoFetchedKey } from "../blockInfoFetch";

// 機械の搬入が継承元のレベルで止まっている理由と、届いて待っている品。取得できない間は取得状態だけを持つ
// Why a machine's inflow is stalled on the inheritance source level, and the item that arrived and waits; while unavailable it carries only the fetch state
const MachineInputStallFetchedDataSchema = z.object({
  ...blockInfoFetchedKey,
  reason: z.enum(["mixedLevel", "aboveCraftCap"]),
  itemId: z.number().int().positive(),
}).strict();
export const MachineInputStallDataSchema = z.discriminatedUnion("fetchState", [MachineInputStallFetchedDataSchema, BlockInfoUnavailableSchema]);
