import { describe, it, expect } from "vitest";
import { resolveDirectMoveTarget } from "./inventoryLogic";
import type { SlotData } from "@/bridge/contract/payloadTypes";

const slots = (xs: SlotData[]): SlotData[] => xs;

describe("resolveDirectMoveTarget", () => {
  it("同種スタックが空きありなら優先する", () => {
    const target = slots([
      { itemId: 5, count: 1 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(0);
  });

  it("同種スタックが満杯なら空スロットへ", () => {
    const target = slots([
      { itemId: 5, count: 100 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(1);
  });

  it("maxStack 未指定(undefined)なら同種探索を飛ばし空スロットへ", () => {
    const target = slots([
      { itemId: 5, count: 1 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, undefined)).toBe(1);
  });

  it("移動先が無ければ -1", () => {
    const target = slots([{ itemId: 9, count: 100 }]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(-1);
  });
});
