import { describe, expect, it } from "vitest";
import { FluidSlotDataSchema } from "./inventory";

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
