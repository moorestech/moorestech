import { z } from "zod";

// ベルトの搬出統計。ホストが全窓を送り、Web側はどの窓を見せるかだけを選ぶ。measuredがfalseの窓はまだ埋まっていない
// Belt exit statistics: the host sends every window and the Web side only picks which to show; a window with measured=false is not filled yet
export const BeltThroughputWindowDataSchema = z.object({
  ticks: z.number().int().positive(),
  measured: z.boolean(),
  exitCount: z.number().int().nonnegative(),
  maxCount: z.number().nonnegative(),
});
export const BeltThroughputDataSchema = z.object({
  currentSpeed: z.number().int().nonnegative(),
  windows: z.array(BeltThroughputWindowDataSchema).min(1),
  // 古い順の直近1秒ごとの搬出数。埋まった秒だけなので9個未満もある
  // Latest per-second exit counts, oldest first; only filled seconds, so fewer than nine can appear
  recentSecondCounts: z.array(z.number().int().nonnegative()).max(9),
  maxCountPerSecond: z.number().nonnegative(),
});

