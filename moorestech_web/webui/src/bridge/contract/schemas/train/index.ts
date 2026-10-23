import { z } from "zod";

export const TrainPlatformModeSchema = z.enum(["loadToTrain", "unloadToPlatform"]);
export const TrainPlatformDataSchema = z.object({
  mode: TrainPlatformModeSchema,
  itemSlotCount: z.number().int().nonnegative().optional(),
});

// 時刻表は駅ブロック座標で対象を固定し、表示名は独立して保持する
// The timetable identifies station blocks by position and carries names only for display
const TrainStationPositionSchema = z.object({ x: z.number().int(), y: z.number().int(), z: z.number().int() });
export const TrainTimetableStationSchema = z.object({ position: TrainStationPositionSchema, name: z.string() });
// 停車駅は駅一覧に入線方向（端）を足した形。端はホストの正本で、UIからの新規追加時だけ固定値を積む
// A stop is a station plus the arrival side; the host owns the side and the UI only fixes it for new additions
const TrainTimetableStopSideSchema = z.enum(["front", "back"]);
export const TrainTimetableStopSchema = TrainTimetableStationSchema.extend({ side: TrainTimetableStopSideSchema });
export const TrainTimetableDataSchema = z.object({
  trainUnitId: z.string(),
  isAutoRun: z.boolean(),
  currentIndex: z.number().int(),
  stops: z.array(TrainTimetableStopSchema),
  stations: z.array(TrainTimetableStationSchema),
});
// 取得状態はC#が判定して kind で届く。読み込み中と取得不可を「値の有無」1ビットに畳まない
// C# decides the fetch state and sends it as the kind; loading and unavailable are not folded into value presence
export const TrainTimetableStateSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("loading") }).strict(),
  z.object({ kind: z.literal("unavailable") }).strict(),
  z.object({ kind: z.literal("ready"), data: TrainTimetableDataSchema }).strict(),
]);
export const TrainStationDetailSchema = z.object({ name: z.string() });

