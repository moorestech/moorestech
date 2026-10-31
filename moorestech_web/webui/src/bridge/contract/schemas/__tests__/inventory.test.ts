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
    [{ fetchState: "fetched", kind: "enclosed", room }, true],
    [{ fetchState: "fetched", kind: "enclosed", room: outRoom, haltReason: "classOut" }, true],
    [{ fetchState: "fetched", kind: "enclosed", room: { ...outRoom, className: "" } }, false],
    [{ fetchState: "fetched", kind: "enclosed", room: { ...room, className: undefined } }, false],
    [{ fetchState: "fetched", kind: "enclosed", room: { ...room, isOut: false } }, false],
    [{ fetchState: "fetched", kind: "openings", openingCount: 3 }, true],
    [{ fetchState: "fetched", kind: "noEnclosure", haltReason: "notEnclosed" }, true],
    [{ fetchState: "fetched", kind: "tooLarge", maxRoomVolume: 4096 }, true],
    [{ fetchState: "fetched", kind: "detecting" }, true],
    [{ fetchState: "fetched", kind: "openings" }, false],
    [{ fetchState: "fetched", kind: "openings", openingCount: 3, room }, false],
    [{ fetchState: "fetched", kind: "enclosed", room, haltReason: "operable" }, false],
    [{ fetchState: "fetched", kind: "sealed" }, false],
    [{ kind: "detecting" }, false],
    [{ fetchState: "fetched" }, false],
    [{ fetchState: "notFetched" }, true],
    [{ fetchState: "transportFailed" }, true],
    [{ fetchState: "unreadable", kind: "detecting" }, false],
    [{ fetchState: "loading" }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(CleanRoomDetailDataSchema.safeParse(value).success).toBe(expected);
  });
});

describe("MachineInputStallDataSchema", () => {
  // 理由は2種だけ、アイテムIDは正の整数、余計なキーは拒否する
  // Only the two reasons, a positive integer item ID, and no extra keys
  it.each([
    [{ fetchState: "fetched", reason: "mixedLevel", itemId: 12 }, true],
    [{ fetchState: "fetched", reason: "aboveCraftCap", itemId: 12 }, true],
    [{ fetchState: "fetched", reason: "full", itemId: 12 }, false],
    [{ fetchState: "fetched", reason: "mixedLevel" }, false],
    [{ fetchState: "fetched", reason: "mixedLevel", itemId: 0 }, false],
    [{ fetchState: "fetched", reason: "mixedLevel", itemId: 1.5 }, false],
    [{ fetchState: "fetched", reason: "mixedLevel", itemId: 12, level: 2 }, false],
    [{ fetchState: "unsupportedTarget" }, true],
    [{ fetchState: "vanished", reason: "mixedLevel", itemId: 12 }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(MachineInputStallDataSchema.safeParse(value).success).toBe(expected);
  });
});

describe("CleanRoomYieldDataSchema", () => {
  // 役割ごとに自分の値だけを受け、割合は0〜1、グレードは1以上の整数
  // Each role accepts only its own values; rates lie in 0..1 and the grade is an integer of at least 1
  it.each([
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 2, promotionChance: 0.9 }, true],
    [{ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.02 }, true],
    [{ fetchState: "fetched", role: "dicing", dieLossRate: 0.03 }, true],
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 2 }, false],
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 0, promotionChance: 0.9 }, false],
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 1.5, promotionChance: 0.9 }, false],
    [{ fetchState: "fetched", role: "dicing", dieLossRate: 1.5 }, false],
    [{ fetchState: "fetched", role: "dicing", dieLossRate: 0.03, layerFailRate: 0.02 }, false],
    [{ fetchState: "fetched", role: "packaging" }, false],
    // 出力待ちの確定済み抽選は親と同じ役割の値だけを持つ
    // A fixed draw waiting for output carries only the parent role's values
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 1, promotionChance: 0, pendingDraw: { maxGrade: 2, promotionChance: 0.9 } }, true],
    [{ fetchState: "fetched", role: "dicing", dieLossRate: 0.03, pendingDraw: { dieLossRate: 0.15 } }, true],
    [{ fetchState: "fetched", role: "dicing", dieLossRate: 0.03, pendingDraw: { layerFailRate: 0.02 } }, false],
    [{ fetchState: "fetched", role: "waferGrade", maxGrade: 1, promotionChance: 0, pendingDraw: { maxGrade: 2 } }, false],
    [{ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.02, pendingDraw: { layerFailRate: 1.2 } }, false],
    [{ fetchState: "notFetched", pendingDraw: { dieLossRate: 0.15 } }, false],
    [{ fetchState: "unreadable" }, true],
    [{ fetchState: "transportFailed", role: "dicing", dieLossRate: 0.03 }, false],
  ])("%j の受理は %s", (value, expected) => {
    expect(CleanRoomYieldDataSchema.safeParse(value).success).toBe(expected);
  });
});
