// 実コンポーネントで編集/同期境界を検証（非通信）
// Exercise real components (no transport) to verify edit/sync boundaries
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BlockInventoryData, TrainTimetableData } from "@/bridge";

vi.mock("@/bridge", () => ({ dispatchAction: vi.fn().mockResolvedValue(true) }));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Stack: "div", Text: "span", ScrollArea: "div",
}));
vi.mock("../BlockItemGrid", () => ({ default: "mock-grid" }));

import { dispatchAction } from "@/bridge";
import TrainInventoryBody from "./TrainInventoryBody";
import TrainTimetableSection from "./TrainTimetableSection";
import TrainStationNameSection from "../details/station/TrainStationNameSection";

const a = { position: { x: 1, y: 0, z: 1 }, name: "A" };
const b = { position: { x: 2, y: 0, z: 2 }, name: "B" };
const aStop = { ...a, side: "back" as const };
const bStop = { ...b, side: "back" as const };
const timetable: TrainTimetableData = {
  trainUnitId: "train", isAutoRun: false, currentIndex: 0, stops: [aStop], stations: [a, b],
};
const readyTimetable = { status: "ready", data: timetable } as const;
type TrainData = Extract<BlockInventoryData, { source: "train" }>;
function trainData(state: TrainData["timetable"]): TrainData {
  return { open: true, source: "train", blockType: "Train", identifier: "car", itemSlots: [], fluidSlots: [], timetable: state };
}

function button(tree: ReactTestRenderer, id: string) {
  return tree.root.findAll((node) => node.type === "button" && node.props["data-testid"] === id)[0];
}
function click(tree: ReactTestRenderer, id: string) {
  act(() => button(tree, id).props.onClick());
}
function render(data: TrainTimetableData) {
  let tree!: ReactTestRenderer;
  act(() => { tree = create(createElement(TrainTimetableSection, { timetable: data })); });
  return tree;
}
function currentRows(tree: ReactTestRenderer) {
  return tree.root.findAll((node) => node.type === "li" && node.props["data-current"] === "true");
}

beforeEach(() => vi.clearAllMocks());

describe("train timetable UI", () => {
  it("sends one replacement only on Apply and immediately sends auto-run changes", () => {
    const tree = render(timetable);
    click(tree, "train-timetable-station-2_0_2-add");
    click(tree, "train-timetable-stop-1-up");
    expect(dispatchAction).not.toHaveBeenCalled();
    expect(currentRows(tree)).toHaveLength(0);
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenCalledTimes(1);
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.replace", { stops: [{ ...b.position, side: "back" }, { ...a.position, side: "back" }] });
    click(tree, "train-timetable-auto-run-on");
    expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.set_auto_run", { enabled: true });
  });

  it("keeps ON disabled until the server has stops, even with locally added stops", () => {
    const empty = { ...timetable, stops: [], currentIndex: -1 };
    const tree = render(empty);
    click(tree, "train-timetable-station-1_0_1-add");
    expect(button(tree, "train-timetable-auto-run-on").props.disabled).toBe(true);
    act(() => tree.update(createElement(TrainTimetableSection, { timetable })));
    expect(button(tree, "train-timetable-auto-run-on").props.disabled).toBeFalsy();
  });

  it("follows clean snapshots and current index but preserves unsent edits", () => {
    const tree = render(timetable);
    const updated = { ...timetable, stops: [aStop, bStop], currentIndex: 1 };
    act(() => tree.update(createElement(TrainTimetableSection, { timetable: updated })));
    expect(currentRows(tree)[0].props["data-testid"]).toBe("train-timetable-stop-1");
    click(tree, "train-timetable-stop-0-remove");
    act(() => tree.update(createElement(TrainTimetableSection, { timetable: { ...updated, stops: [aStop] } })));
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", { stops: [{ ...b.position, side: "back" }] });
  });

  it("discards unapplied edits when the train panel unmounts", () => {
    const data = { open: true, source: "train", blockType: "Train", identifier: "car", itemSlots: [], timetable: readyTimetable } as const;
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-station-2_0_2-add");
    act(() => tree.unmount());
    act(() => { tree = create(createElement(TrainInventoryBody, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-apply");
    const replaceCalls = vi.mocked(dispatchAction).mock.calls.filter(([type]) => type === "train_timetable.replace");
    expect(replaceCalls).toEqual([["train_timetable.replace", { stops: [{ ...a.position, side: "back" }] }]]);
  });

  it("keeps unapplied edits while switching tabs", () => {
    const data = { open: true, source: "train", blockType: "Train", identifier: "car", itemSlots: [], timetable: readyTimetable } as const;
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-station-2_0_2-add");
    click(tree, "train-tab-inventory");
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.replace", { stops: [{ ...a.position, side: "back" }, { ...b.position, side: "back" }] });
  });

  it("asks C# to fetch only when the timetable tab is selected", () => {
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData({ status: "loading" }) })); });
    expect(dispatchAction).not.toHaveBeenCalled();
    click(tree, "train-tab-timetable");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.open", {});
    click(tree, "train-tab-inventory");
    expect(dispatchAction).toHaveBeenCalledTimes(1);
  });

  it("shows loading and unavailable as distinct states without warning", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData({ status: "loading" }) })); });
    click(tree, "train-tab-timetable");
    const byTestId = (id: string) => tree.root.findAll((node) => node.props["data-testid"] === id);
    expect(byTestId("train-timetable-loading")).toHaveLength(1);
    expect(byTestId("train-timetable-unavailable")).toHaveLength(0);
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData({ status: "unavailable" }) })));
    expect(byTestId("train-timetable-loading")).toHaveLength(0);
    expect(byTestId("train-timetable-unavailable")).toHaveLength(1);
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })));
    expect(byTestId("train-timetable-section")).toHaveLength(1);
    expect(warn).not.toHaveBeenCalled();
    warn.mockRestore();
  });

  it("sends station names only through Set and hides the field for platforms", () => {
    const data = { open: true, source: "block", blockType: "TrainStation", identifier: "station", blockGuid: "g", itemSlots: [], fluidSlots: [], trainStation: { name: "A" } } as const;
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainStationNameSection, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    act(() => tree.root.findByType("input").props.onChange({ currentTarget: { value: "North" } }));
    expect(dispatchAction).not.toHaveBeenCalled();
    click(tree, "train-station-name-apply");
    expect(dispatchAction).toHaveBeenCalledTimes(1);
    expect(dispatchAction).toHaveBeenCalledWith("train_station.set_name", { name: "North" });
    act(() => tree.update(createElement(TrainStationNameSection, { data: { ...data, itemSlots: [], fluidSlots: [], trainStation: undefined } })));
    expect(tree.toJSON()).toBeNull();
  });
});
