// 所持アンカーが「先頭スロットのみ・小文字guid・空/count0除外」の契約を固定する
// Pins the owned-item anchor contract: first slot only, lowercased guid, empty/count-0 excluded
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { ItemMasterEntry, PlayerInventoryData } from "@/bridge";

const host = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  itemMaster: null as Map<number, ItemMasterEntry> | null,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.inventory ? host.inventory : null),
    useItemMaster: () => host.itemMaster,
    dispatchAction: vi.fn(),
  };
});

// スロットはアンカー属性だけ観測したいので、Mantine依存を避けた素の要素へ置き換える
// Only the anchor attribute matters here, so replace the slot with a bare element free of Mantine dependencies
vi.mock("@/shared/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/ui")>();
  return {
    ...actual,
    ItemSlot: (props: object) => createElement("span", { ...props, "data-mock": "item-slot" }),
  };
});

import InventoryPanel from "./index";

const slot = (itemId: number, count: number) => ({ itemId, count });

function renderAnchors() {
  const renderer = create(createElement(InventoryPanel));
  return renderer.root
    .findAll((node) => typeof node.props["data-tutorial-anchor"] === "string")
    .map((node) => node.props["data-tutorial-anchor"] as string);
}

describe("InventoryPanel の所持アンカー", () => {
  it("先頭スロットのみ小文字guidでアンカーを名乗り、空/count0のスロットは無視される", () => {
    host.itemMaster = new Map<number, ItemMasterEntry>([
      [7, { itemId: 7, itemGuid: "A0000000-0000-4000-8000-000000000001", maxStack: 100 }],
    ]);
    host.inventory = {
      mainSlots: [slot(0, 0), slot(7, 0), slot(3, 1), slot(7, 5)],
      grab: slot(0, 0),
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    };

    expect(renderAnchors()).toEqual(["inventory.item-a0000000-0000-4000-8000-000000000001"]);
  });
});
