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
  it.each([
    ["status", "unknown"], ["cablePoolAmount", -1], ["cablePoolAmount", 0.5],
    ["cablesPerElevator", 0], ["climbProgress", -0.1], ["climbProgress", 1.1],
    ["satelliteInputSlots", [-1]], ["outputSlots", [1.5]], ["outputSlots", undefined],
    ["satelliteInputSlots", undefined], ["extraField", true],
  ])("%s=%s を拒否する", (key, value) => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, spaceElevator: { ...detail, [key]: value } }).success).toBe(false);
  });
});
