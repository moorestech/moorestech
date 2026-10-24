import { z } from "zod";
import { BlockInfoUnavailableSchema, blockInfoFetchedKey } from "../blockInfoFetch";

// 室内機械・清浄機の部屋欄。種別ごとに持つフィールドが決まる
// Room section of interior machines and purifiers; each kind fixes which fields it carries
const CleanRoomHaltReasonSchema = z.enum(["notEnclosed", "classOut"]);
const CleanRoomAirFilterDataSchema = z.object({
  x: z.number().int(),
  y: z.number().int(),
  z: z.number().int(),
  powerRatio: z.number().nonnegative(),
  filterItemCount: z.number().int().nonnegative(),
  filterRemainingRatio: z.number().min(0).max(1),
}).strict();
const cleanRoomSummaryCommon = {
  concentration: z.number().nonnegative(),
  airChangeRate: z.number().nonnegative(),
  volume: z.number().int().nonnegative(),
  airFilters: z.array(CleanRoomAirFilterDataSchema),
};
// 見出しの種別はサーバーのクラス種別の写し。クラス名は class のときだけ持つ
// The headline kind mirrors the server's class kind; only class carries a class name
export const CleanRoomSummaryDataSchema = z.discriminatedUnion("headline", [
  z.object({ headline: z.literal("class"), className: z.string().min(1), ...cleanRoomSummaryCommon }).strict(),
  z.object({ headline: z.literal("classOut"), ...cleanRoomSummaryCommon }).strict(),
]);
// 取得済みの部屋欄は種別ごとに持つフィールドが決まり、取得できない間は取得状態だけを持つ
// A fetched room section fixes its fields per kind; while unavailable it carries only the fetch state
const CleanRoomFetchedDetailDataSchema = z.discriminatedUnion("kind", [
  z.object({ ...blockInfoFetchedKey, kind: z.literal("enclosed"), room: CleanRoomSummaryDataSchema, haltReason: CleanRoomHaltReasonSchema.optional() }).strict(),
  z.object({ ...blockInfoFetchedKey, kind: z.literal("openings"), openingCount: z.number().int().positive(), haltReason: CleanRoomHaltReasonSchema.optional() }).strict(),
  z.object({ ...blockInfoFetchedKey, kind: z.literal("noEnclosure"), haltReason: CleanRoomHaltReasonSchema.optional() }).strict(),
  z.object({ ...blockInfoFetchedKey, kind: z.literal("tooLarge"), maxRoomVolume: z.number().int().positive(), haltReason: CleanRoomHaltReasonSchema.optional() }).strict(),
  z.object({ ...blockInfoFetchedKey, kind: z.literal("detecting"), haltReason: CleanRoomHaltReasonSchema.optional() }).strict(),
]);
export const CleanRoomDetailDataSchema = z.discriminatedUnion("fetchState", [CleanRoomFetchedDetailDataSchema, BlockInfoUnavailableSchema]);

// 室内機械の選択工程の抽選の中身。役割ごとに載る値が決まる
// Draw details of an interior machine's selected process; each role fixes which values it carries
const CleanRoomRateSchema = z.number().min(0).max(1);
const CleanRoomYieldFetchedDataSchema = z.discriminatedUnion("role", [
  z.object({ ...blockInfoFetchedKey, role: z.literal("waferGrade"), maxGrade: z.number().int().positive(), promotionChance: CleanRoomRateSchema }).strict(),
  z.object({ ...blockInfoFetchedKey, role: z.literal("layerExposure"), layerFailRate: CleanRoomRateSchema }).strict(),
  z.object({ ...blockInfoFetchedKey, role: z.literal("dicing"), dieLossRate: CleanRoomRateSchema }).strict(),
]);
export const CleanRoomYieldDataSchema = z.discriminatedUnion("fetchState", [CleanRoomYieldFetchedDataSchema, BlockInfoUnavailableSchema]);
