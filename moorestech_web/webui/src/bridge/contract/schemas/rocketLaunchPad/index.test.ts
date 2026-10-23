import { describe, expect, it } from "vitest";
import { BlockInventoryOpenSchema } from "../inventory";
import { loadFixture } from "../../wireFixtures.test-helper";

const fixture = loadFixture("block_inventory_rocket_launch_pad.json") as Record<string, unknown>;
const detail = fixture.rocketLaunchPad as Record<string, unknown>;

describe("rocket launch pad wire boundary", () => {
  it("accepts the shared fixture and allows initial pending details", () => {
    expect(BlockInventoryOpenSchema.safeParse(fixture).success).toBe(true);
    const { rocketLaunchPad: _pending, ...pending } = fixture;
    expect(BlockInventoryOpenSchema.safeParse(pending).success).toBe(true);
  });
  it("rejects details on a different block type", () => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, blockType: "Chest" }).success).toBe(false);
  });
  it.each([
    { phase: "unknown" }, { waitReason: "unknown" }, { progress: -0.1 }, { progress: 1.1 },
    { progress: Number.NaN }, { materialSlots: [-1] }, { payloadSlots: [0.5] }, { outputSlots: ["2"] }, { extra: true },
  ])("rejects malformed state %j", (bad) => {
    expect(BlockInventoryOpenSchema.safeParse({ ...fixture, rocketLaunchPad: { ...detail, ...bad } }).success).toBe(false);
  });
});

// 各群の範囲外添字は描画前の受信境界で拒否する
// Reject each group's out-of-bounds indices before rendering
it.each(["materialSlots", "payloadSlots", "outputSlots"])("rejects out of range %s", (group) => {
  expect(BlockInventoryOpenSchema.safeParse({ ...fixture, itemSlots: [{ itemId: 0, count: 0 }], rocketLaunchPad: { ...detail, materialSlots: [], payloadSlots: [], outputSlots: [], [group]: [1] } }).success).toBe(false);
});
