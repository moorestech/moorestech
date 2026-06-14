import { describe, it, expect } from "vitest";
import { pickUpPayload, placePayload, resolveBlockComponent } from "./blockLogic";
import ChestInventory from "./ChestInventory";
import TankInventory from "./TankInventory";
import GenericBlockInventory from "./GenericBlockInventory";

describe("pickUpPayload", () => {
  it("block スロット→grab へ count ごと拾う payload を作る", () => {
    expect(pickUpPayload(3, 7)).toEqual({
      from: { area: "block", slot: 3 },
      to: { area: "grab", slot: 0 },
      count: 7,
    });
  });
});

describe("placePayload", () => {
  it("grab→block スロットへ grabCount ごと置く payload を作る", () => {
    expect(placePayload(2, 5)).toEqual({
      from: { area: "grab", slot: 0 },
      to: { area: "block", slot: 2 },
      count: 5,
    });
  });
});

describe("resolveBlockComponent", () => {
  it("Chest(実マスタ値) は ChestInventory を返す", () => {
    expect(resolveBlockComponent("Chest")).toBe(ChestInventory);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest")).toBe(GenericBlockInventory);
  });
  it("tank は TankInventory を返す（INV-6 で登録）", () => {
    expect(resolveBlockComponent("tank")).toBe(TankInventory);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown")).toBe(GenericBlockInventory);
  });
});
