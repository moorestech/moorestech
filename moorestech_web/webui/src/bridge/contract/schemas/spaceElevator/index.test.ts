import { describe, expect, it } from "vitest";
import { BlockInventoryOpenSchema } from "../inventory";
import { loadFixture } from "../../wireFixtures.test-helper";

const fixture = loadFixture("block_inventory_space_elevator.json") as Record<string, unknown>;
const detail = fixture.spaceElevator as Record<string, unknown>;

describe("SpaceElevator receive boundary", () => {
  it("別のblockTypeへの詳細混入を拒否する", () => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, blockType: "Chest" }).success).toBe(false);
  });
  it("入口詰まりを受理する", () => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, spaceElevator: { ...detail, status: "inletBlocked" } }).success).toBe(true);
  });
  it("詳細未着を許容する", () => {
    const { spaceElevator: pending, ...rest } = fixture;
    expect(pending).toBeDefined();
    expect(BlockInventoryOpenSchema.safeParse(rest).success).toBe(true);
  });
  it("成功在庫は入口と出口の添字を全て含む", () => {
    expect(BlockInventoryOpenSchema.safeParse(fixture).success).toBe(true);
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, itemSlots: [] }).success).toBe(false);
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, spaceElevator: { ...detail, outputSlots: [3] } }).success).toBe(false);
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, spaceElevator: { ...detail, satelliteInputSlots: [3], slotBindings: [{ slot: 3, acceptedItemIds: [5] }] } }).success).toBe(false);
  });
  it("失敗在庫は詳細を省き空在庫と失敗理由を受理する", () => {
    const { spaceElevator: _detail, ...rest } = fixture;
    const result = BlockInventoryOpenSchema.parse({ ...rest, error: "openFailed", itemSlots: [] });
    expect(result.error).toBe("openFailed");
    expect(result.spaceElevator).toBeUndefined();
  });
  it.each([
    ["status", "unknown"], ["cablePoolAmount", -1], ["cablePoolAmount", 0.5],
    ["cablesPerElevator", 0], ["climbProgress", -0.1], ["climbProgress", 1.1],
    ["satelliteInputSlots", [-1]], ["outputSlots", [1.5]], ["outputSlots", undefined],
    ["satelliteInputSlots", undefined], ["extraField", true],
    ["slotBindings", undefined], ["slotBindings", []], ["slotBindings", [{ slot: 1, acceptedItemIds: [5] }]],
  ])("%s=%s を拒否する", (key, value) => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, spaceElevator: { ...detail, [key]: value } }).success).toBe(false);
  });
});
