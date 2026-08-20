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
      grab: slot(0, 0),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    };
    bridge.blockInventory = null;
    bridge.itemMaster = new Map([[1, { itemId: 1, itemGuid: "87000000-0000-4000-8000-000000000001", maxStack: 100 }]]);
  });

  it("クリック時の inventory・block slots・maxStack で移動を計画する", () => {
    // レンダー後の更新を模擬し、最新値だけで分配数が決まる状態へ変える
    // Simulate post-render updates so only the latest values determine distribution counts
    bridge.inventory = {
      mainSlots: [slot(1, 5)],
      grab: slot(0, 0),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    };
    bridge.blockInventory = {
      open: true,
      source: "block",
      blockType: "chest",
      identifier: "block-1",
      blockGuid: "85000000-0000-4000-8000-000000000001",
      itemSlots: [slot(1, 8), slot(0, 0)],
      fluidSlots: [],
    };
    bridge.itemMaster = new Map([[1, { itemId: 1, itemGuid: "87000000-0000-4000-8000-000000000002", maxStack: 10 }]]);

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
      grab: slot(9, 3),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
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
      grab: slot(9, 3),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    };
    slotActions.onRightEnter({ area: "main", slot: 0 });

    expect(bridge.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
      from: { area: "grab", slot: 0 },
      to: { area: "main", slot: 0 },
      count: 1,
    });
  });

  // grab保持中の左クリックは対象スロットの中身で分岐する（uGUI の LeftClickDown と同じ判断）
  // A grab-held left click branches on the target slot's contents, matching uGUI's LeftClickDown
  describe("grab保持中の左クリック", () => {
    const grabbing = (target: { itemId: number; count: number }): PlayerInventoryData => ({
      mainSlots: [target],
      grab: slot(9, 4),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    });

    it("別IDの中身ありスロットへは全量moveを送る（サーバーが入れ替える）", () => {
      bridge.inventory = grabbing(slot(1, 5));

      slotActions.onLeftDown({ area: "main", slot: 0 }, false);

      expect(bridge.dispatchAction).toHaveBeenCalledTimes(1);
      expect(bridge.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
        from: { area: "grab", slot: 0 },
        to: { area: "main", slot: 0 },
        count: 4,
      });
    });

    it("同IDの中身ありスロットへも全量moveを送る", () => {
      bridge.inventory = grabbing(slot(9, 5));

      slotActions.onLeftDown({ area: "main", slot: 0 }, false);

      expect(bridge.dispatchAction).toHaveBeenCalledTimes(1);
      expect(bridge.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
        from: { area: "grab", slot: 0 },
        to: { area: "main", slot: 0 },
        count: 4,
      });
    });

    // 空スロットはスプリットドラッグ開始で即時送信しない（配分の中身は splitDrag.test.ts が持つ）
    // An empty slot starts split-drag and sends nothing yet; the distribution itself is covered by splitDrag.test.ts
    it("空スロットは従来どおりスプリットドラッグを開始し何も送らない", () => {
      bridge.inventory = grabbing(slot(0, 0));

      slotActions.onLeftDown({ area: "main", slot: 0 }, false);

      expect(bridge.dispatchAction).not.toHaveBeenCalled();
    });
  });

  // 枠数が縮んだ直後のクリックは描画済みの ref だけが残り、最新 snapshot には対象が無い
  // Right after the slot count shrinks, only the rendered ref survives while the latest snapshot has no such slot
  describe("範囲外スロットへの操作", () => {
    beforeEach(() => {
      bridge.inventory = {
        mainSlots: [slot(1, 5)],
        grab: slot(0, 0),
        equipment: [],
        selectedEquipment: -1,
        equipmentSelectionConfirmationRevision: 0,
      };
    });

    it.each([
      ["equipment", { area: "equipment", slot: 0 } as const],
      ["main", { area: "main", slot: 9 } as const],
    ])("%s の範囲外 ref は例外を投げず何も送らない", (_, ref) => {
      expect(() => slotActions.onLeftDown(ref, false)).not.toThrow();
      expect(() => slotActions.onLeftDown(ref, true)).not.toThrow();
      expect(() => slotActions.onRightDown(ref)).not.toThrow();
      expect(() => slotActions.onDoubleClick(ref)).not.toThrow();
      expect(() => slotActions.onLeftEnter(ref)).not.toThrow();
      expect(() => slotActions.onRightEnter(ref)).not.toThrow();
      expect(bridge.dispatchAction).not.toHaveBeenCalled();
    });

    it("grab 保持中でも範囲外スロットへは何も送らない", () => {
      bridge.inventory = {
        mainSlots: [slot(0, 0)],
        grab: slot(9, 4),
        equipment: [],
        selectedEquipment: -1,
        equipmentSelectionConfirmationRevision: 0,
      };
      const ref = { area: "equipment", slot: 0 } as const;

      expect(() => slotActions.onLeftDown(ref, false)).not.toThrow();
      expect(() => slotActions.onRightDown(ref)).not.toThrow();
      expect(() => slotActions.onRightEnter(ref)).not.toThrow();
      expect(bridge.dispatchAction).not.toHaveBeenCalled();
    });
  });
});
