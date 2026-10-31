import { z } from "zod";

// 親種別が決まっている詳細欄の許可表
// Allowed parents for detail sections restricted to specific block types
const DETAIL_SECTION_PARENT_BLOCK_TYPES = {
  spaceElevator: ["SpaceElevatorBase"],
  rocketLaunchPad: ["RocketLaunchPad"],
  gearToElectric: ["GearToElectricGenerator"],
  electricToGear: ["ElectricToGearGenerator"],
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
