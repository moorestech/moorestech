// @vitest-environment jsdom
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BeltThroughputData, BlockInventoryOpen } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string | number>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@/shared/uiState", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/uiState")>()),
  readActiveLayer: () => "blockInventory",
  isTextInputElement: () => false,
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import BeltThroughputSection from "../BeltThroughputSection";

const beltThroughput: BeltThroughputData = {
  currentSpeed: 18,
  windows: [
    { ticks: 90, measured: true, exitCount: 4, maxCount: 4.5 },
    { ticks: 180, measured: true, exitCount: 9, maxCount: 9 },
    { ticks: 360, measured: false, exitCount: 0, maxCount: 18 },
  ],
};

function render(belt: BeltThroughputData | undefined) {
  const data = { open: true, source: "block", blockType: "GearBeltConveyor", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], beltThroughput: belt } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(BeltThroughputSection, { data })); });
  return tree;
}

function textOf(tree: ReturnType<typeof render>, testId: string): string {
  const node = tree.root.findAll((n) => n.props["data-testid"] === testId)[0];
  return String(node.props.children);
}

function press(key: string) {
  act(() => { window.dispatchEvent(new KeyboardEvent("keydown", { key })); });
}

describe("BeltThroughputSection", () => {
  it("renders nothing without belt data", () => {
    expect(render(undefined).toJSON()).toBeNull();
  });

  // 既定は180tick窓。個数・最大・割合の行と分間行を出す
  // The default is the 180-tick window, with the count/max/percent row and the per-minute row
  it("shows the 180-tick window by default", () => {
    const tree = render(beltThroughput);
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"9","count":9,"max":"9","percent":100}');
    expect(textOf(tree, "belt-throughput-per-minute")).toBe('ui.blockInventory.itemsPerMinute|{"itemsPerMinute":"60.0"}');
    expect(tree.root.findAll((n) => n.props["data-testid"] === "belt-throughput-window-hint")).toHaveLength(1);
  });

  // →で長い窓へ、←で短い窓へ。計測中の窓は分間行を持たない
  // Right moves to a longer window, left to a shorter one; a measuring window has no per-minute row
  it("switches windows with the arrow keys", () => {
    const tree = render(beltThroughput);
    press("ArrowRight");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputMeasuring|{"seconds":"18"}');
    expect(tree.root.findAll((n) => n.props["data-testid"] === "belt-throughput-per-minute")).toHaveLength(0);
    press("ArrowLeft");
    press("ArrowLeft");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"4.5","count":4,"max":"4.5","percent":89}');
    press("ArrowLeft");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"4.5","count":4,"max":"4.5","percent":89}');
  });
});
