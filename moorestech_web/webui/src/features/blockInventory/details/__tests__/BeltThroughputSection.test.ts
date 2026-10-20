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
vi.mock("@/shared/ui", () => ({
  BarSeries: (props: object) => createElement("mock-bars", props),
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
  recentSecondCounts: [1, 0, 1],
  maxCountPerSecond: 1,
};

function render(belt: BeltThroughputData | undefined) {
  const data = { open: true, source: "block", blockType: "GearBeltConveyor", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], beltThroughput: belt } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(BeltThroughputSection, { data })); });
  return tree;
}

function nodeOf(tree: ReturnType<typeof render>, testId: string) {
  return tree.root.findAll((n) => n.props["data-testid"] === testId)[0];
}

function textOf(tree: ReturnType<typeof render>, testId: string): string {
  return String(nodeOf(tree, testId).props.children);
}

function press(key: string) {
  act(() => { window.dispatchEvent(new KeyboardEvent("keydown", { key })); });
}

describe("BeltThroughputSection", () => {
  it("renders nothing without belt data", () => {
    expect(render(undefined).toJSON()).toBeNull();
  });

  // 既定は180tick窓。個数・最大・割合の行、分間行、9本の棒(足りない秒は先頭側が空)。ヒント文は出さない
  // The default is the 180-tick window with the count/max/percent row, the per-minute row and nine bars (missing seconds empty at the front); no hint text
  it("shows the 180-tick window and nine per-second bars by default", () => {
    const tree = render(beltThroughput);
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"9","count":9,"max":"9","percent":100}');
    expect(textOf(tree, "belt-throughput-per-minute")).toBe('ui.blockInventory.itemsPerMinute|{"itemsPerMinute":"60.0"}');
    expect(nodeOf(tree, "belt-throughput-bars").props.values).toEqual([null, null, null, null, null, null, 1, 0, 1]);
    expect(tree.root.findAll((n) => n.props["data-testid"] === "belt-throughput-window-hint")).toHaveLength(0);
  });

  // →で長い窓へ、←で短い窓へ。計測中の窓は分間行を持たない。棒は窓に依らない
  // Right moves to a longer window, left to a shorter one; a measuring window has no per-minute row; the bars do not depend on the window
  it("switches windows with the arrow keys", () => {
    const tree = render(beltThroughput);
    press("ArrowRight");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputMeasuring|{"seconds":"18"}');
    expect(tree.root.findAll((n) => n.props["data-testid"] === "belt-throughput-per-minute")).toHaveLength(0);
    expect(nodeOf(tree, "belt-throughput-bars").props.values).toEqual([null, null, null, null, null, null, 1, 0, 1]);
    press("ArrowLeft");
    press("ArrowLeft");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"4.5","count":4,"max":"4.5","percent":89}');
    press("ArrowLeft");
    expect(textOf(tree, "belt-throughput-window")).toBe('ui.blockInventory.beltThroughputWindow|{"seconds":"4.5","count":4,"max":"4.5","percent":89}');
  });
});
