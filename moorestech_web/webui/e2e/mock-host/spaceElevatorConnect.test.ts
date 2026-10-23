import { describe, expect, it } from "vitest";
import { applySpaceElevatorConnect } from "./detailActions";
import type { BlockInventoryWireData } from "./types/blockInventoryWireData";

function base(): Extract<BlockInventoryWireData, { open: true; source: "block" }> {
  return {
    open: true, source: "block", blockType: "SpaceElevatorBase", identifier: "block:20",
    blockGuid: "99999999-9999-4999-8999-999999999999", itemSlots: [], fluidSlots: [],
    spaceElevator: { connected: false, cablePoolAmount: 150, cablesPerElevator: 100, climbProgress: 0, status: "readyToConnect" },
  };
}

describe("space elevator mock response", () => {
  it("closed and wrong-type requests retain their reasons", () => {
    expect(applySpaceElevatorConnect({ open: false })).toEqual({ ok: false, error: "block_not_open" });
    expect(applySpaceElevatorConnect({ ...base(), blockType: "Chest" })).toEqual({ ok: false, error: "invalid_block_type" });
  });
  it("subtracts once and reports already connected separately", () => {
    const block = base();
    expect(applySpaceElevatorConnect(block)).toEqual({ ok: true });
    expect(block.spaceElevator?.cablePoolAmount).toBe(50);
    expect(applySpaceElevatorConnect(block)).toEqual({ ok: false, error: "already_connected" });
    expect(block.spaceElevator?.cablePoolAmount).toBe(50);
  });
  it("insufficient cable leaves the original state untouched", () => {
    const block = base();
    block.spaceElevator!.cablePoolAmount = 99;
    const before = structuredClone(block);
    expect(applySpaceElevatorConnect(block)).toEqual({ ok: false, error: "insufficient_cable" });
    expect(block).toEqual(before);
  });
});
