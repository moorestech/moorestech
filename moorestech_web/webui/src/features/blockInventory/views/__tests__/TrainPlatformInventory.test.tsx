import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";

// 在庫スロットは実物を使い、外部データと周辺操作だけを隔離する
// Render real inventory slots while isolating external data and surrounding controls
vi.mock("@/bridge", async (original) => ({
  ...(await original<typeof import("@/bridge")>()),
  useFluidMaster: () => new Map([["water", { color: "#0088ff" }]]),
  useConnectionStatus: () => "connected",
}));
vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Tooltip: ({ children }: { children: unknown }) => children,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("section", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("main", props, children as never),
  Text: ({ children }: { children: unknown }) => createElement("p", null, children as never),
}));
vi.mock("../../details/TrainPlatformSection", () => ({ default: () => null }));
vi.mock("../../details/station/TrainStationNameSection", () => ({ default: () => null }));
import TrainPlatformInventory from "../TrainPlatformInventory";

describe("TrainPlatformInventory", () => {
  it("PF自身の充填スロット・常時テキストを更新し、空でも実スロットを残す", () => {
    const data: BlockInventoryOpen = {
      open: true, source: "block", blockType: "TrainFluidPlatform", identifier: "block:1", blockGuid: "platform",
      itemSlots: [], fluidSlots: [{ kind: "filled", fluidGuid: "water", amount: 250, capacity: 1000 }],
    };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(TrainPlatformInventory, { data })); });
    expect(tree.root.findByType("img").props.src).toBe("/api/fluid-icons/water.png");
    expect(tree.root.findAllByType("p").map((node) => node.props.children)).toEqual(["fluid.water.name", "250 / 1,000"]);
    expect(tree.root.findAllByProps({ "data-testid": "fluid-slot" })).toHaveLength(1);
    act(() => { tree.update(createElement(TrainPlatformInventory, { data: { ...data, fluidSlots: [{ kind: "filled", fluidGuid: "water", amount: 700, capacity: 1000 }] } })); });
    expect(tree.root.findAllByType("p").map((node) => node.props.children)).toEqual(["fluid.water.name", "700 / 1,000"]);
    expect(tree.root.findByProps({ "data-testid": "fluid-slot" }).findByType("span").props.children).toBe("700");
    act(() => { tree.update(createElement(TrainPlatformInventory, { data: { ...data, fluidSlots: [{ kind: "empty", capacity: 1000 }] } })); });
    expect(tree.root.findAllByProps({ "data-testid": "fluid-slot" })).toHaveLength(1);
    expect(tree.root.findAllByType("img")).toHaveLength(0);
    expect(tree.root.findAllByType("p")).toHaveLength(0);
    act(() => { tree.unmount(); });
  });
});
