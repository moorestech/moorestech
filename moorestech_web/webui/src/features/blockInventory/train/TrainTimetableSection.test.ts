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
const readyTimetable = { kind: "ready", data: timetable } as const;
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
function stopRows(tree: ReactTestRenderer) {
  return tree.root.findAll((node) => node.type === "li");
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

  // ON時のOFFクリック経路はテストで守られていなかった（isAutoRunを反転しても赤くならない）
  // The ON-to-OFF click path was untested (flipping isAutoRun did not turn anything red)
  it("sends auto-run off from the ON state", () => {
    const tree = render({ ...timetable, isAutoRun: true });
    expect(button(tree, "train-timetable-auto-run-on").props["data-selected"]).toBe("true");
    click(tree, "train-timetable-auto-run-off");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.set_auto_run", { enabled: false });
  });

  // 非受理は編集を権威へ戻す（dirtyのまま再同期されなくなるのを防ぐ）
  // A rejected replacement returns the draft to authority (it must not stay dirty forever)
  it("returns the draft to the server timetable when the replacement is not accepted", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const tree = render(timetable);
    click(tree, "train-timetable-station-2_0_2-add");
    expect(stopRows(tree)).toHaveLength(2);
    vi.mocked(dispatchAction).mockResolvedValueOnce(false);
    await act(async () => { button(tree, "train-timetable-apply").props.onClick(); });
    expect(stopRows(tree)).toHaveLength(1);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  // 差し戻し先はクリック時ではなくawait解決時の権威（古い権威で固定するとdirtyのまま残る）
  // The revert target is the authority at resolution time, not at click time (an old one would leave it dirty)
  it("reverts to the authority that arrived while the send was in flight", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const tree = render(timetable);
    click(tree, "train-timetable-station-2_0_2-add");
    let settle!: (accepted: boolean) => void;
    vi.mocked(dispatchAction).mockReturnValueOnce(new Promise<boolean>((resolve) => { settle = resolve; }));
    click(tree, "train-timetable-apply");
    act(() => tree.update(createElement(TrainTimetableSection, { timetable: { ...timetable, stops: [bStop] } })));
    await act(async () => { settle(false); });
    expect(stopRows(tree)).toHaveLength(1);
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", { stops: [{ ...b.position, side: "back" }] });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
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
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-station-2_0_2-add");
    act(() => tree.unmount());
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-apply");
    const replaceCalls = vi.mocked(dispatchAction).mock.calls.filter(([type]) => type === "train_timetable.replace");
    expect(replaceCalls).toEqual([["train_timetable.replace", { stops: [{ ...a.position, side: "back" }] }]]);
  });

  it("keeps unapplied edits while switching tabs", () => {
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })); });
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-station-2_0_2-add");
    click(tree, "train-tab-inventory");
    click(tree, "train-tab-timetable");
    click(tree, "train-timetable-apply");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.replace", { stops: [{ ...a.position, side: "back" }, { ...b.position, side: "back" }] });
  });

  it("asks C# to fetch only when the timetable tab is selected", () => {
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData({ kind: "loading" }) })); });
    expect(dispatchAction).not.toHaveBeenCalled();
    click(tree, "train-tab-timetable");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.open", {});
    click(tree, "train-tab-inventory");
    expect(dispatchAction).toHaveBeenCalledTimes(1);
  });

  it("asks C# to fetch on tab select even when the timetable is already ready", () => {
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })); });
    click(tree, "train-tab-timetable");
    expect(dispatchAction).toHaveBeenCalledWith("train_timetable.open", {});
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData(readyTimetable) })));
    expect(dispatchAction).toHaveBeenCalledTimes(1);
  });

  it("re-asks C# when a reset fetch arrives as loading while the timetable tab stays selected", () => {
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData({ kind: "loading" }) })); });
    click(tree, "train-tab-timetable");
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData({ kind: "unavailable" }) })));
    expect(dispatchAction).toHaveBeenCalledTimes(1);
    // 同一車両の閉→開が畳まれ再マウントされなくても、読み込み中の配信で取得を頼み直す
    // Even without a remount after a folded same-car close/open, a loading publish asks for the fetch again
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData({ kind: "loading" }) })));
    expect(dispatchAction).toHaveBeenCalledTimes(2);
    expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.open", {});
  });

  it("shows loading and unavailable as distinct states without warning", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    let tree!: ReactTestRenderer;
    act(() => { tree = create(createElement(TrainInventoryBody, { data: trainData({ kind: "loading" }) })); });
    click(tree, "train-tab-timetable");
    const byTestId = (id: string) => tree.root.findAll((node) => node.props["data-testid"] === id);
    expect(byTestId("train-timetable-loading")).toHaveLength(1);
    expect(byTestId("train-timetable-unavailable")).toHaveLength(0);
    act(() => tree.update(createElement(TrainInventoryBody, { data: trainData({ kind: "unavailable" }) })));
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
