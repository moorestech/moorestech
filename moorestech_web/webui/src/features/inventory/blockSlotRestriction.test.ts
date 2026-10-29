import { describe, expect, it } from "vitest";
import type { BlockInventoryData } from "@/bridge";
import { BlockInventoryOpenSchema } from "../../bridge/contract/schemas/inventory";
import { loadFixture } from "../../bridge/contract/wireFixtures.test-helper";
import { planPlayerLeftClick, type PlayerSlotContext } from "@/shared/itemMove";
import { resolveBlockSlotRestriction } from "./blockSlotRestriction";

// ホスト共有フィクスチャを受信境界で読む
// Build values through the receive boundary from the host fixtures shared with the C# WireContract tests
const rocketPad = (): BlockInventoryData => BlockInventoryOpenSchema.parse(loadFixture("block_inventory_rocket_launch_pad.json"));
const spaceElevator = (): BlockInventoryData => BlockInventoryOpenSchema.parse(loadFixture("block_inventory_space_elevator.json"));

const PAYLOAD_ITEM = 4;
const SATELLITE_ITEM = 5;

// 持物slot0全量のShift配分の帰結
// Outcome planned for a Shift-click allocating main slot 0
function shiftPlan(block: BlockInventoryData, itemId: number, count: number) {
  if (!block.open) throw new Error("fixture must be open");
  const ctx: PlayerSlotContext = {
    inventory: { mainSlots: [{ itemId, count }], grab: { itemId: 0, count: 0 }, equipment: [], selectedEquipment: 0, equipmentSelectionConfirmationRevision: 0 },
    maxStack: 1000,
    blockItemSlots: block.itemSlots,
    blockSlotRestriction: resolveBlockSlotRestriction(block, itemId),
  };
  return planPlayerLeftClick({ area: "main", slot: 0 }, { itemId, count }, true, ctx);
}

// 配分の宛先スロット列
// Destination slots of the allocation
function shiftDestinations(block: BlockInventoryData, itemId: number, count: number): number[] {
  const plan = shiftPlan(block, itemId, count);
  if (plan.kind !== "actions") throw new Error("shift click must plan actions");
  return plan.actions.map((action) => {
    if (action.type !== "block_inventory.move_item") throw new Error(`unexpected ${action.type}`);
    return action.payload.to.slot;
  });
}

describe("発射台のShift配分", () => {
  it("積荷は受け付ける積荷枠だけへ流し、先頭の空き資材枠や出力枠を選ばない", () => {
    expect(shiftDestinations(rocketPad(), PAYLOAD_ITEM, 10)).toEqual([1]);
  });

  it("どの枠も受け付けないアイテムは送らず拒否として返す", () => {
    expect(shiftPlan(rocketPad(), 99, 10)).toEqual({ kind: "noAcceptingBlockSlot" });
  });

  it("受け付け情報が未着なら無制限へ倒さず送らない", () => {
    const block = rocketPad();
    if (!block.open || block.source !== "block") throw new Error("fixture must be an open block");
    const { rocketLaunchPad: _pending, ...rest } = block;
    expect(resolveBlockSlotRestriction(rest, PAYLOAD_ITEM)).toEqual({ kind: "bound", candidateIndices: [] });
  });
});

describe("宇宙エレベーターのShift配分", () => {
  it("運べる衛星は入口だけへ流し、出口へは入れない", () => {
    const block = spaceElevator();
    if (!block.open) throw new Error("fixture must be open");
    const withSlots = { ...block, itemSlots: [{ itemId: 0, count: 0 }, { itemId: 0, count: 0 }, { itemId: 0, count: 0 }] };
    expect(shiftDestinations(withSlots, SATELLITE_ITEM, 3)).toEqual([0]);
    expect(shiftPlan(withSlots, 99, 3)).toEqual({ kind: "noAcceptingBlockSlot" });
  });

  it("受け付け情報が未着なら送らない", () => {
    const block = spaceElevator();
    if (!block.open || block.source !== "block") throw new Error("fixture must be an open block");
    const { spaceElevator: _pending, ...rest } = block;
    expect(resolveBlockSlotRestriction(rest, SATELLITE_ITEM)).toEqual({ kind: "bound", candidateIndices: [] });
  });
});

describe("束縛を持たないブロック", () => {
  it("チェストは無制限のまま", () => {
    const chest = { ...rocketPad(), blockType: "Chest" } as BlockInventoryData;
    if (!chest.open || chest.source !== "block") throw new Error("fixture must be an open block");
    const { rocketLaunchPad: _detail, ...rest } = chest;
    expect(resolveBlockSlotRestriction(rest, PAYLOAD_ITEM)).toEqual({ kind: "unrestricted" });
  });
});
