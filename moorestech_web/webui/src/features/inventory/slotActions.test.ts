import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BlockInventoryData, ItemMasterEntry, PlayerInventoryData } from "@/bridge";

const bridge = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  blockInventory: null as BlockInventoryData | null,
  itemMaster: null as Map<number, ItemMasterEntry> | null,
  dispatchAction: vi.fn(),
}));

vi.mock("@/bridge", () => ({
  Topics: {
    inventory: "inventory.snapshot",
    blockInventory: "block_inventory.snapshot",
  },
  readTopic: (topic: string) => topic === "inventory.snapshot" ? bridge.inventory : bridge.blockInventory,
  readItemMaster: () => bridge.itemMaster,
  dispatchAction: bridge.dispatchAction,
}));

import { slotActions } from "./slotActions";

const slot = (itemId: number, count: number) => ({ itemId, count });

describe("slotActions", () => {
  beforeEach(() => {
    bridge.dispatchAction.mockReset();
    bridge.inventory = {
      mainSlots: [slot(1, 5)],
      hotbarSlots: [slot(0, 0)],
      grab: slot(0, 0),
      selectedHotbar: 0,
    };
    bridge.blockInventory = null;
    bridge.itemMaster = new Map([[1, { itemId: 1, name: "item", maxStack: 100 }]]);
  });

  it("クリック時の inventory・block slots・maxStack で移動を計画する", () => {
    // レンダー後の更新を模擬し、最新値だけで分配数が決まる状態へ変える
    // Simulate post-render updates so only the latest values determine distribution counts
    bridge.inventory = {
      mainSlots: [slot(1, 5)],
      hotbarSlots: [slot(0, 0)],
      grab: slot(0, 0),
      selectedHotbar: 0,
    };
    bridge.blockInventory = {
      open: true,
      blockType: "chest",
      identifier: "block-1",
      blockName: "Chest",
      itemSlots: [slot(1, 8), slot(0, 0)],
      fluidSlots: [],
    };
    bridge.itemMaster = new Map([[1, { itemId: 1, name: "latest", maxStack: 10 }]]);

    slotActions.onLeftDown({ area: "main", slot: 0 }, true);

    expect(bridge.dispatchAction).toHaveBeenNthCalledWith(1, "block_inventory.move_item", {
      from: { area: "main", slot: 0 },
      to: { area: "block", slot: 0 },
      count: 2,
    });
    expect(bridge.dispatchAction).toHaveBeenNthCalledWith(2, "block_inventory.move_item", {
      from: { area: "main", slot: 0 },
      to: { area: "block", slot: 1 },
      count: 3,
    });
  });

  it("右クリック時の最新 grab 数で分割操作を選ぶ", () => {
    bridge.inventory = {
      mainSlots: [slot(1, 5)],
      hotbarSlots: [],
      grab: slot(9, 3),
      selectedHotbar: 0,
    };

    slotActions.onRightDown({ area: "main", slot: 0 });

    expect(bridge.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
      from: { area: "grab", slot: 0 },
      to: { area: "main", slot: 0 },
      count: 1,
    });
  });
});
