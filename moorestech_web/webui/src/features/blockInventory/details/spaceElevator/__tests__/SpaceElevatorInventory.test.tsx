import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";
import { L } from "@/shared/i18n";
import { Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";

const gestures = vi.hoisted(() => ({ onLeftDown: vi.fn(), onRightDown: vi.fn(), onDoubleClick: vi.fn() }));
vi.mock("../../../useBlockSlotGestures", () => ({ useBlockSlotGestures: () => gestures }));
vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-slot", props),
  SlotGrid: ({ children, testId }: { children: unknown; testId: string }) => createElement("mock-grid", { "data-testid": testId }, children as never),
}));
vi.mock("../SpaceElevatorSection", () => ({ default: () => null }));
vi.mock("../../NetworkSections", () => ({ ElectricNetworkSection: () => null }));

import SpaceElevatorInventory from "../../../views/SpaceElevatorInventory";

// 全枠が空でも役割を示し、入力DTOの並びを変えずに元indexを操作へ渡す
// Show roles even with empty slots and forward original indices without reordering the input DTO
describe("SpaceElevatorInventory", () => {
  it.each([false, true])("空スロット=%sでも入口1枠と全出口を識別し元データを保持する", (empty) => {
    const data: BlockInventoryOpen = {
      open: true, source: "block", blockType: "SpaceElevatorBase", identifier: "block:20",
      blockGuid: "99999999-9999-4999-8999-999999999999", fluidSlots: [],
      itemSlots: Array.from({ length: 4 }, (_, i) => ({ itemId: empty ? 0 : i + 1, count: empty ? 0 : i + 2 })),
    };
    const before = structuredClone(data);
    data.itemSlots.forEach(Object.freeze);
    Object.freeze(data.itemSlots);
    Object.freeze(data);
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(SpaceElevatorInventory, { data })); });
    const labels = tree.root.findAllByType(Text).map((node) => node.props.children);
    expect(labels).toEqual([L.ui.blockInventory.spaceElevatorSatelliteInput, L.ui.blockInventory.spaceElevatorResultOutput]);
    const input = tree.root.findByProps({ "data-testid": "space-elevator-input-grid" });
    const output = tree.root.findByProps({ "data-testid": "space-elevator-output-grid" });
    expect(input.findAllByType(ItemSlot)).toHaveLength(1);
    expect(output.findAllByType(ItemSlot)).toHaveLength(3);

    // 分割した全枠で左・Shift・右・収集のindexが保持される
    // Every split slot preserves its index for left, Shift, right, and collect gestures
    const slots = [...input.findAllByType(ItemSlot), ...output.findAllByType(ItemSlot)];
    slots.forEach((slot, index) => {
      vi.clearAllMocks();
      expect(slot.props.itemId).toBe(before.itemSlots[index].itemId);
      expect(slot.props.count).toBe(before.itemSlots[index].count);
      act(() => {
        slot.props.onLeftDown(false);
        slot.props.onLeftDown(true);
        slot.props.onRightDown();
        slot.props.onDoubleClick();
      });
      expect(gestures.onLeftDown.mock.calls).toEqual([[index, false], [index, true]]);
      expect(gestures.onRightDown.mock.calls).toEqual([[index]]);
      expect(gestures.onDoubleClick.mock.calls).toEqual([[index]]);
    });
    expect(data).toEqual(before);
    act(() => { tree.unmount(); });
  });
});
