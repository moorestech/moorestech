import { z } from "zod";

// 照会欄（部屋・抽選の中身・止まった理由）が取得できていない間の形。ドメインの値は1つも持たない
// Shape of a query section (room, draw details, stall) while unavailable; it carries no domain value at all
export const BlockInfoUnavailableSchema = z.object({
  fetchState: z.enum(["notFetched", "transportFailed", "unreadable", "unsupportedTarget", "vanished"]),
}).strict();

// 取得済みの欄が各枝に持つ判別キー
// Discriminator key every fetched branch carries
export const blockInfoFetchedKey = { fetchState: z.literal("fetched") };
