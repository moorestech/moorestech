import { z } from "zod";

// 照会欄ごとに、載ってよい親ブロックの種別。ホストが欄を追う判定（BlockNetworkInfoCache.Track）と同じ集合
// Parent block types each query section may ride on; the same sets as the host's tracking decision (BlockNetworkInfoCache.Track)
const DETAIL_SECTION_PARENT_BLOCK_TYPES = {
  cleanRoom: ["CleanRoomMachine", "CleanRoomAirFilter"],
  cleanRoomYield: ["CleanRoomMachine"],
  machineInputStall: ["ElectricMachine", "GearMachine", "CleanRoomMachine"],
} as const;

type DetailSectionKey = keyof typeof DETAIL_SECTION_PARENT_BLOCK_TYPES;
type DetailSectionCarrier = { blockType: string } & Partial<Record<DetailSectionKey, unknown>>;

// 対象外の親（Chest 等）へ機械専用の欄が混入した payload を受信境界で拒否する。欄の未着は許容する
// Reject at the receive boundary a payload carrying a machine-only section on a non-target parent (Chest etc.); a pending section is allowed
export function refineDetailSectionParents(data: DetailSectionCarrier, ctx: z.RefinementCtx): void {
  for (const key of Object.keys(DETAIL_SECTION_PARENT_BLOCK_TYPES) as DetailSectionKey[]) {
    const parents: readonly string[] = DETAIL_SECTION_PARENT_BLOCK_TYPES[key];
    if (data[key] !== undefined && !parents.includes(data.blockType))
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: [key], message: `${key} requires one of ${parents.join(", ")}` });
  }
}
