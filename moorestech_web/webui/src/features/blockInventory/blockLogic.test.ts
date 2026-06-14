import { describe, it, expect } from "vitest";
import { pickUpPayload, placePayload, resolveBlockComponent } from "./blockLogic";
import ChestInventory from "./ChestInventory";
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
  it("chest は ChestInventory を返す", () => {
    expect(resolveBlockComponent("chest")).toBe(ChestInventory);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("tank")).toBe(GenericBlockInventory);
    expect(resolveBlockComponent("unknown")).toBe(GenericBlockInventory);
  });
});
