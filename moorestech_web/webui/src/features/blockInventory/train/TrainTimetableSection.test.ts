// 通信せず実コンポーネントを操作し、編集と同期の境界を検証する
// Exercise real components without transport to verify editing and synchronization boundaries
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TrainTimetableData } from "@/bridge";

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
const timetable: TrainTimetableData = {
  trainUnitId: "train", isAutoRun: false, currentIndex: 0, stops: [a], stations: [a, b],
};

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
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.replace", { stations: [b.position, a.position] });
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
    const updated = { ...timetable, stops: [a, b], currentIndex: 1 };
    act(() => tree.update(createElement(TrainTimetableSection, { timetable: updated })));
    expect(currentRows(tree)[0].props["data-testid"]).toBe("train-timetable-stop-1");
    click(tree, "train-timetable-stop-0-remove");
    act(() => tree.update(createElement(TrainTimetableSection, { timetable: { ...updated, stops: [a] } })));
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", { stations: [b.position] });
  });

  it("discards unapplied edits when the train panel unmounts", () => {
    const data = { open: true, source: "train", blockType: "Train", identifier: "car", itemSlots: [], timetable } as const;
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-station-2_0_2-add");
    act(() => tree.unmount());
    act(() => { tree = create(createElement(TrainInventoryBody, { data: { ...data, itemSlots: [], fluidSlots: [] } })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenCalledTimes(1);
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.replace", { stations: [a.position] });
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
