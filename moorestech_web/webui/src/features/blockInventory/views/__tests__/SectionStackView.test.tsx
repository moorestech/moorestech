// @vitest-environment jsdom
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
vi.mock("@/bridge", async (original) => ({
  ...(await original<typeof import("@/bridge")>()),
  useFluidMaster: () => new Map([["water", { color: "#0088ff" }]]),
  useConnectionStatus: () => "connected",
}));
vi.mock("@mantine/core", () => ({
  Tooltip: ({ children }: { children: unknown }) => children,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("../../BlockItemGrid", () => ({
  default: ({ testId }: { testId: string }) => createElement("div", { "data-testid": testId }),
}));
vi.mock("@/shared/ui", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/ui")>()),
  HoverTooltip: ({ children }: { children: unknown }) => createElement("mock-hover-tooltip", null, children as never),
}));

import SectionStackView from "../SectionStackView";

function render(data: BlockInventoryOpen) {
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(SectionStackView, { data, fillsPanelHeight: false })); });
  return tree;
}

function dataFor(blockType: string, itemSlots: BlockInventoryOpen["itemSlots"]): BlockInventoryOpen {
  return { open: true, source: "block", blockType, identifier: "block:1", blockGuid: "g", itemSlots, fluidSlots: [{ kind: "empty", capacity: 100 }], progress: 0.4 } as unknown as BlockInventoryOpen;
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
    ["CleanRoomMachine", null, "machine-fluid-slots", false, false],
    ["CleanRoomAirFilter", "clean-room-filter-grid", null, true, false],
    ["unknown", "generic-block-grid", "generic-block-fluids", false, true],
  ])("%s の既存表示を保つ", (blockType, itemGridTestId, fluidRowTestId, renderEmptyGrid, showFluidProgress) => {
    const tree = render(dataFor(blockType, []));
    const emptyGrids = tree.root.findAll((n) => n.type === "div" && n.props["data-testid"] !== undefined && n.props["data-testid"] !== "fluid-slot" && n.props["data-testid"] !== "progress-arrow-bar");
    expect(emptyGrids.map((n) => n.props["data-testid"])).toEqual(renderEmptyGrid ? [itemGridTestId] : []);
    const fluids = tree.root.findAll((n) => String(n.type) === "mock-group" && n.props["data-testid"] !== undefined);
    expect(fluids.map((n) => n.props["data-testid"])).toEqual(fluidRowTestId ? [fluidRowTestId] : []);
    if (fluidRowTestId) expect(tree.root.findAllByProps({ "data-testid": "progress-arrow-bar" })).toHaveLength(showFluidProgress ? 1 : 0);
    act(() => { tree.unmount(); });
    const populated = render(dataFor(blockType, [{ itemId: 1, count: 1 }]));
    expect(populated.root.findAll((n) => n.type === "div" && n.props["data-testid"] !== undefined && n.props["data-testid"] !== "fluid-slot" && n.props["data-testid"] !== "progress-arrow-bar").map((n) => n.props["data-testid"])).toEqual(itemGridTestId ? [itemGridTestId] : []);
    act(() => { populated.unmount(); });
  });

  it("パイプは液体だけを進捗なしで描画する", () => {
    const data = dataFor("FluidPipe", []);
    data.fluidSlots = [{ kind: "filled", fluidGuid: "water", amount: 40, capacity: 100 }];
    const tree = render(data);
    expect(tree.root.findAllByProps({ "data-testid": "progress-arrow-bar" })).toHaveLength(0);
    expect(tree.root.findByType("img").props.src).toBe("/api/fluid-icons/water.png");
    expect(tree.root.findAll((n) => String(n.type) === "mock-text").map((n) => Array.isArray(n.props.children) ? n.props.children.join("") : n.props.children)).toEqual(["fluid.water.name|undefined", "40 / 100"]);
    expect(tree.root.findAllByProps({ "data-testid": "generic-block-grid" })).toHaveLength(0);
    act(() => { tree.update(createElement(SectionStackView, { data: { ...data, fluidSlots: [{ kind: "filled", fluidGuid: "water", amount: 60, capacity: 100 }] }, fillsPanelHeight: false })); });
    expect(tree.root.findByProps({ "data-testid": "fluid-slot" }).findByType("span").props.children).toBe("60");
    expect(tree.root.findAll((n) => String(n.type) === "mock-text").map((n) => n.props.children)).toContain("60 / 100");
    act(() => { tree.unmount(); });
  });

  it("空のパイプは名前や容量を表示せず空スロットを残す", () => {
    const data = dataFor("FluidPipe", []);
    data.fluidSlots = [{ kind: "empty", capacity: 100 }];
    const tree = render(data);
    expect(tree.root.findByProps({ "data-testid": "fluid-pipe-fluid-slots-slot-0" })).toBeDefined();
    expect(tree.root.findAll((n) => String(n.type) === "mock-text")).toHaveLength(0);
    expect(tree.root.findAllByProps({ "data-testid": "fluid-slot" })).toHaveLength(1);
    expect(tree.root.findAllByType("img")).toHaveLength(0);
    act(() => { tree.unmount(); });
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
    expect(tree.root.findAll((n) => n.type === "div" && n.props["data-testid"] !== undefined && n.props["data-testid"] !== "fluid-slot" && n.props["data-testid"] !== "progress-arrow-bar" && n.props["data-testid"] !== undefined)).toHaveLength(0);
    act(() => { tree.unmount(); });
  });

  it("部屋欄はセクション列の先頭に描く", () => {
    const data = dataFor("CleanRoomAirFilter", [{ itemId: 12, count: 5 }]);
    data.cleanRoom = { fetchState: "fetched", kind: "openings", openingCount: 3 };
    const tree = render(data);
    const ids = tree.root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
    expect(ids.slice(0, 3)).toEqual(["clean-room-section", "clean-room-headline", "clean-room-filter-grid"]);
    act(() => { tree.unmount(); });
  });

  it("搬入停止欄は機械欄より上に赤字で描く", () => {
    const data = dataFor("GearMachine", []);
    data.machineInputStall = { fetchState: "fetched", reason: "mixedLevel", itemId: 12 };
    const tree = render(data);
    const row = tree.root.findByProps({ "data-testid": "machine-input-stall" });
    expect(row.props["data-insufficient"]).toBe(true);
    act(() => { tree.unmount(); });
  });

  it("抽選の中身欄は部屋欄のすぐ下に描く", () => {
    const data = dataFor("CleanRoomMachine", []);
    data.cleanRoom = { fetchState: "fetched", kind: "enclosed", room: { headline: "class", className: "A", concentration: 0.1, airChangeRate: 2, volume: 26, airFilters: [{ x: 1, y: 1, z: 1, powerRatio: 1, filterItemCount: 5, filterRemainingRatio: 0.5 }] } };
    data.cleanRoomYield = { fetchState: "fetched", role: "dicing", dieLossRate: 0.03 };
    const tree = render(data);
    const ids = tree.root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
    expect(ids.filter((id: string) => id.startsWith("clean-room"))).toEqual(["clean-room-section", "clean-room-headline", "clean-room-air", "clean-room-air-filter-0", "clean-room-yield-section", "clean-room-yield-die-loss-rate"]);
    act(() => { tree.unmount(); });
  });

  it("照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く", () => {
    const empty = render(dataFor("CleanRoomMachine", []));
    expect(empty.root.findAll((n) => typeof n.props["data-testid"] === "string" && /^(clean-room|machine-input-stall)/.test(n.props["data-testid"]))).toEqual([]);
    act(() => { empty.unmount(); });
    const data = dataFor("CleanRoomMachine", []);
    data.cleanRoom = { fetchState: "notFetched" };
    data.cleanRoomYield = { fetchState: "notFetched" };
    data.machineInputStall = { fetchState: "notFetched" };
    const tree = render(data);
    const ids = tree.root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
    expect(ids.slice(0, 3)).toEqual(["clean-room-fetch-state", "clean-room-yield-fetch-state", "machine-input-stall-fetch-state"]);
    act(() => { tree.unmount(); });
  });
});
