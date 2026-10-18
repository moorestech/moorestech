import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryData } from "@/bridge";

vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  ScrollArea: ({ children }: { children: unknown }) => createElement("main", null, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("section", props, children as never),
  Group: ({ children }: { children: unknown }) => createElement("section", null, children as never),
  Text: ({ children }: { children: unknown }) => createElement("p", null, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ModeSwitch: () => null,
  FluidSlotRow: ({ testId }: { testId: string }) => createElement("aside", { "data-testid": testId }),
}));
vi.mock("../BlockItemGrid", () => ({ default: () => createElement("div", { "data-testid": "items" }) }));
vi.mock("./TrainTimetableSection", () => ({ default: () => null }));
import TrainInventoryBody from "./TrainInventoryBody";

type TrainData = Extract<BlockInventoryData, { source: "train" }>;
describe("TrainInventoryBody", () => {
  it("液体貨車は名前と量容量を表示し、再取得した量へ更新する", () => {
    const data: TrainData = { blockType: "Train", identifier: "car:1", source: "train", open: true, itemSlots: [], fluidSlots: [{ kind: "filled", fluidGuid: "water", amount: 300, capacity: 1000 }], timetable: { kind: "unavailable" } };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(TrainInventoryBody, { data })); });
    expect(tree.root.findAllByProps({ "data-testid": "items" })).toHaveLength(0);
    expect(tree.root.findAllByProps({ "data-testid": "train-inventory-fluids-slot-0" })).toHaveLength(1);
    expect(tree.root.findAllByType("p").map((p) => p.props.children)).toEqual(["fluid.water.name", "300 / 1,000"]);
    act(() => { tree.update(createElement(TrainInventoryBody, { data: { ...data, fluidSlots: [{ kind: "filled", fluidGuid: "water", amount: 500, capacity: 1000 }] } })); });
    expect(tree.root.findAllByType("p").map((p) => p.props.children)).toEqual(["fluid.water.name", "500 / 1,000"]);
    act(() => { tree.unmount(); });
  });
});
