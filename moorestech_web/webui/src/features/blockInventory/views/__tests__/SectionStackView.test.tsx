import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";

// 親と発電・歯車・網セクションは実物で描画する
// Render the real parent and generation, gear, and network sections
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, unknown>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("../../BlockItemGrid", () => ({
  default: ({ testId }: { testId: string }) => createElement("div", { "data-testid": testId }),
}));
vi.mock("@/shared/ui", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/ui")>()),
  FluidSlotRow: ({ testId, progress }: { testId: string; progress: number | null }) => createElement("aside", { "data-testid": testId, progress }),
}));

import SectionStackView from "../SectionStackView";

function render(data: BlockInventoryOpen) {
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(SectionStackView, { data, fillsPanelHeight: false })); });
  return tree;
}

function dataFor(blockType: string, itemSlots: BlockInventoryOpen["itemSlots"]): BlockInventoryOpen {
  return { open: true, source: "block", blockType, identifier: "block:1", blockGuid: "g", itemSlots, fluidSlots: [], progress: 0.4 } as unknown as BlockInventoryOpen;
}

describe("SectionStackView", () => {
  // 内部設定関数ではなく、描画されたグリッドと液体行を検査する
  // Inspect rendered grids and fluid rows instead of the internal config function
  it.each([
    ["Chest", "chest-grid", null, true, true],
    ["ElectricMachine", null, "machine-fluid-slots", false, false],
    ["GearMachine", null, "machine-fluid-slots", false, false],
    ["ElectricGenerator", "generator-fuel-grid", null, true, true],
    ["FuelGearGenerator", "generator-fuel-grid", null, true, true],
    ["SimpleGearGenerator", "generator-fuel-grid", null, true, true],
    ["ElectricMiner", "miner-output-grid", null, true, true],
    ["GearMiner", "gear-miner-output-grid", null, true, true],
    ["unknown", "generic-block-grid", "generic-block-fluids", false, true],
  ])("%s の既存表示を保つ", (blockType, itemGridTestId, fluidRowTestId, renderEmptyGrid, showFluidProgress) => {
    const tree = render(dataFor(blockType, []));
    const emptyGrids = tree.root.findAll((n) => n.type === "div");
    expect(emptyGrids.map((n) => n.props["data-testid"])).toEqual(renderEmptyGrid ? [itemGridTestId] : []);
    const fluids = tree.root.findAll((n) => n.type === "aside");
    expect(fluids.map((n) => n.props["data-testid"])).toEqual(fluidRowTestId ? [fluidRowTestId] : []);
    if (fluidRowTestId) expect(fluids[0].props.progress).toBe(showFluidProgress ? 0.4 : null);
    act(() => { tree.unmount(); });
    const populated = render(dataFor(blockType, [{ itemId: 1, count: 1 }]));
    expect(populated.root.findAll((n) => n.type === "div").map((n) => n.props["data-testid"])).toEqual(itemGridTestId ? [itemGridTestId] : []);
    act(() => { populated.unmount(); });
  });

  it("親から状態・供給・歯車・電力網・歯車網を順に描画する", () => {
    const data = dataFor("GearToElectricGenerator", []);
    data.gearToElectric = { generationState: "generating", suppliedPower: 72.4, maxGeneratedPower: 100 };
    data.gear = { role: "consumer", currentRpm: 120, currentTorque: 100, baseRpm: 120 };
    data.electricNetwork = { totalGeneratePower: 100, totalRequiredPower: 72.4, consumerCount: 1, powerRate: 1 };
    data.gearNetwork = { totalGenerateGearPower: 100, totalRequiredGearPower: 72, stopReason: "none" };
    const tree = render(data);
    const ids = tree.root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
    expect(ids).toEqual([
      "gear-to-electric-section", "gear-to-electric-state", "gear-to-electric-supplied-power",
      "gear-section", "gear-torque", "gear-rpm", "electric-network-section", "gear-network-section",
    ]);
    expect(tree.root.findByProps({ "data-testid": "gear-to-electric-state" }).props.children).toBe("ui.blockInventory.gearToElectricStateGenerating|undefined");
    expect(tree.root.findByProps({ "data-testid": "gear-to-electric-supplied-power" }).props.children).toBe('ui.blockInventory.gearToElectricSuppliedPower|{"current":"72","max":"100"}');
    expect(tree.root.findAll((n) => n.type === "div")).toHaveLength(0);
    act(() => { tree.unmount(); });
  });
});
