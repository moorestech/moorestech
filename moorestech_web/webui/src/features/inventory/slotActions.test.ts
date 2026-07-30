import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BlockInventoryData, ItemMasterEntry, PlayerInventoryData } from "@/bridge";

const bridge = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  blockInventory: null as BlockInventoryData | null,
  itemMaster: null as Map<number, ItemMasterEntry> | null,
  dispatchAction: vi.fn(),
}));

// topic 名は実契約の Topics をそのまま使う。mock 固有の別名は本番との drift を隠す
// Use the real contract's Topics; mock-only aliases would hide drift from production
vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    readTopic: (topic: string) => topic === actual.Topics.inventory ? bridge.inventory : bridge.blockInventory,
    readItemMaster: () => bridge.itemMaster,
    dispatchAction: bridge.dispatchAction,
  };
});

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
    bridge.itemMaster = new Map([[1, { itemId: 1, itemGuid: "item-guid", maxStack: 100 }]]);
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
      source: "block",
      blockType: "chest",
      identifier: "block-1",
      blockGuid: "block-guid",
      itemSlots: [slot(1, 8), slot(0, 0)],
      fluidSlots: [],
    };
    bridge.itemMaster = new Map([[1, { itemId: 1, itemGuid: "latest-guid", maxStack: 10 }]]);

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

  it("右ドラッグ進入は grab がある時だけ1個配置する", () => {
    slotActions.onRightEnter({ area: "main", slot: 0 });
    expect(bridge.dispatchAction).not.toHaveBeenCalled();

    bridge.inventory = {
      mainSlots: [slot(0, 0)],
      hotbarSlots: [],
      grab: slot(9, 3),
      selectedHotbar: 0,
    };
    slotActions.onRightEnter({ area: "main", slot: 0 });

    expect(bridge.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
      from: { area: "grab", slot: 0 },
      to: { area: "main", slot: 0 },
      count: 1,
    });
  });
});
