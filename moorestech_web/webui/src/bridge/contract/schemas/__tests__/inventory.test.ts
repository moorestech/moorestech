import { describe, expect, it } from "vitest";
import { FluidSlotDataSchema } from "../inventory";
import { CleanRoomDetailDataSchema, CleanRoomYieldDataSchema } from "../cleanRoom";
import { MachineInputStallDataSchema } from "../machineInputStall";

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";

describe("FluidSlotDataSchema", () => {
  it("空流体(fluidId=0/amount=0/fluidGuid='')はkind: emptyへ変換する", () => {
    const slot = FluidSlotDataSchema.parse({ fluidId: 0, amount: 0, capacity: 1000, fluidGuid: "" });
    expect(slot).toEqual({ kind: "empty", capacity: 1000 });
  });

  it("充填済み流体はkind: filledへ変換し表示に使うフィールドを保持する", () => {
    const slot = FluidSlotDataSchema.parse({ fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID });
    expect(slot).toEqual({ kind: "filled", amount: 500, capacity: 1000, fluidGuid: FLUID_GUID });
  });

  it("fluidGuidが空文字なのにfluidIdが正の値のpayloadは境界で弾く", () => {
    expect(() => FluidSlotDataSchema.parse({ fluidId: 10, amount: 500, capacity: 1000, fluidGuid: "" })).toThrow();
  });

  it("fluidGuidがあるのにfluidIdが0以下のpayloadは境界で弾く", () => {
    expect(() => FluidSlotDataSchema.parse({ fluidId: 0, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID })).toThrow();
  });

  it("液体を出し切った直後のamount 0はサーバの正常な過渡状態として受理する", () => {
    const slot = FluidSlotDataSchema.parse({ fluidId: 10, amount: 0, capacity: 1000, fluidGuid: FLUID_GUID });
    expect(slot).toEqual({ kind: "filled", amount: 0, capacity: 1000, fluidGuid: FLUID_GUID });
  });
});

describe("CleanRoomDetailDataSchema", () => {
  const airFilters = [{ x: 1, y: 33, z: 5, powerRatio: 1, filterItemCount: 5, filterRemainingRatio: 0.5 }];
  const room = { headline: "class", className: "A", concentration: 0.1, airChangeRate: 2, volume: 74, airFilters };
  const outRoom = { headline: "classOut", concentration: 0.1, airChangeRate: 2, volume: 74, airFilters };

  // 種別ごとに必要なフィールドだけを受け、別種別のフィールドの混入は拒否する
  // Each kind accepts only its own fields and rejects fields of another kind
  it.each([
    [{ kind: "enclosed", room }, true],
    [{ kind: "enclosed", room: outRoom, haltReason: "classOut" }, true],
    [{ kind: "enclosed", room: { ...outRoom, className: "" } }, false],
    [{ kind: "enclosed", room: { ...room, className: undefined } }, false],
    [{ kind: "enclosed", room: { ...room, isOut: false } }, false],
    [{ kind: "openings", openingCount: 3 }, true],
    [{ kind: "noEnclosure", haltReason: "notEnclosed" }, true],
    [{ kind: "tooLarge", maxRoomVolume: 4096 }, true],
    [{ kind: "detecting" }, true],
    [{ kind: "openings" }, false],
    [{ kind: "openings", openingCount: 3, room }, false],
    [{ kind: "enclosed", room, haltReason: "operable" }, false],
    [{ kind: "sealed" }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(CleanRoomDetailDataSchema.safeParse(value).success).toBe(expected);
  });
});

describe("MachineInputStallDataSchema", () => {
  // 理由は2種だけ、アイテムIDは正の整数、余計なキーは拒否する
  // Only the two reasons, a positive integer item ID, and no extra keys
  it.each([
    [{ reason: "mixedLevel", itemId: 12 }, true],
    [{ reason: "aboveCraftCap", itemId: 12 }, true],
    [{ reason: "full", itemId: 12 }, false],
    [{ reason: "mixedLevel" }, false],
    [{ reason: "mixedLevel", itemId: 0 }, false],
    [{ reason: "mixedLevel", itemId: 1.5 }, false],
    [{ reason: "mixedLevel", itemId: 12, level: 2 }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(MachineInputStallDataSchema.safeParse(value).success).toBe(expected);
  });
});

describe("CleanRoomYieldDataSchema", () => {
  // 役割ごとに自分の値だけを受け、割合は0〜1、グレードは1以上の整数
  // Each role accepts only its own values; rates lie in 0..1 and the grade is an integer of at least 1
  it.each([
    [{ role: "waferGrade", maxGrade: 2, promotionChance: 0.9 }, true],
    [{ role: "layerExposure", layerFailRate: 0.02 }, true],
    [{ role: "dicing", dieLossRate: 0.03 }, true],
    [{ role: "waferGrade", maxGrade: 2 }, false],
    [{ role: "waferGrade", maxGrade: 0, promotionChance: 0.9 }, false],
    [{ role: "waferGrade", maxGrade: 1.5, promotionChance: 0.9 }, false],
    [{ role: "dicing", dieLossRate: 1.5 }, false],
    [{ role: "dicing", dieLossRate: 0.03, layerFailRate: 0.02 }, false],
    [{ role: "packaging" }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(CleanRoomYieldDataSchema.safeParse(value).success).toBe(expected);
  });
});
