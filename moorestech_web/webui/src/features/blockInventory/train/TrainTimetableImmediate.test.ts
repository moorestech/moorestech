import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { TrainTimetableData } from "@/bridge";
vi.mock("@/bridge", () => ({ dispatchAction: vi.fn(), DEFAULT_ACTION_TIMEOUT_MS: 5000 }));
vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()), useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({ Stack: "div", Text: "span" }));
import { DEFAULT_ACTION_TIMEOUT_MS, dispatchAction } from "@/bridge";
import TrainTimetableSection from "./TrainTimetableSection";

const a = { name: "A", position: { x: 1, y: 0, z: 1 }, side: "back" as const };
const b = { name: "B", position: { x: 2, y: 0, z: 2 }, side: "back" as const };
const data: TrainTimetableData = {
  trainUnitId: "train", isAutoRun: false, currentIndex: 0, stops: [a], stations: [a, b],
};
let tree: ReactTestRenderer;
const button = (id: string) => tree.root.findAll(n => n.type === "button" && n.props["data-testid"] === id)[0];
const rows = () => tree.root.findAll(n => n.type === "li");
const busy = () => tree.root.findAll(n => n.props["data-testid"] === "train-timetable-section")[0].props["aria-busy"];
function render(value: TrainTimetableData) {
  act(() => { tree = create(createElement(TrainTimetableSection, { timetable: value })); });
}
function publish(stops: TrainTimetableData["stops"]) {
  act(() => tree.update(createElement(TrainTimetableSection, { timetable: { ...data, stops } })));
}
async function click(id: string) {
  expect(button(id).props.disabled).toBeFalsy();
  await act(async () => { button(id).props.onClick(); });
}

beforeEach(() => { vi.mocked(dispatchAction).mockReset().mockResolvedValue(true); });
afterEach(() => { act(() => tree.unmount()); vi.useRealTimers(); vi.restoreAllMocks(); });

it("immediately sends each add, move and remove without an Apply button", async () => {
  render(data);
  expect(button("train-timetable-apply")).toBeUndefined();
  await click("train-timetable-station-2_0_2-add");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", {
    stops: [{ ...a.position, side: "back" }, { ...b.position, side: "back" }],
  });
  publish([a, b]);
  await click("train-timetable-stop-1-up");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", {
    stops: [{ ...b.position, side: "back" }, { ...a.position, side: "back" }],
  });
  publish([b, a]);
  await click("train-timetable-stop-1-remove");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", { stops: [{ ...b.position, side: "back" }] });
  expect(dispatchAction).toHaveBeenCalledTimes(3);
});

it("waits for publication after acceptance so the next edit cannot use stale stops", async () => {
  render(data);
  await click("train-timetable-station-2_0_2-add");
  expect(rows()).toHaveLength(2);
  expect(busy()).toBe(true);
  expect(button("train-timetable-stop-1-up").props.disabled).toBe(true);
  expect(button("train-timetable-station-1_0_1-add").props.disabled).toBe(true);
  publish([a]);
  expect(busy()).toBe(true);
  publish([a, b]);
  expect(busy()).toBe(false);
});

it("waits for acceptance even when the publication arrives first", async () => {
  let settle!: (ok: boolean) => void;
  vi.mocked(dispatchAction).mockReturnValueOnce(new Promise(resolve => { settle = resolve; }));
  render(data);
  await click("train-timetable-station-2_0_2-add");
  publish([a, b]);
  expect(busy()).toBe(true);
  await act(async () => { settle(true); });
  expect(busy()).toBe(false);
});

it("reverts rejected edits to the newest authority and allows another edit", async () => {
  const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
  let settle!: (ok: boolean) => void;
  vi.mocked(dispatchAction).mockReturnValueOnce(new Promise(resolve => { settle = resolve; }));
  render(data);
  await click("train-timetable-station-2_0_2-add");
  publish([b]);
  await act(async () => { settle(false); });
  expect(rows()).toHaveLength(1);
  expect(busy()).toBe(false);
  await click("train-timetable-station-1_0_1-add");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.replace", {
    stops: [{ ...b.position, side: "back" }, { ...a.position, side: "back" }],
  });
  expect(warn).toHaveBeenCalled();
});

it("releases a missing publication after the existing transport timeout", async () => {
  vi.useFakeTimers();
  const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
  render(data);
  await click("train-timetable-station-2_0_2-add");
  publish([a]);
  await act(async () => { vi.advanceTimersByTime(DEFAULT_ACTION_TIMEOUT_MS); });
  expect(dispatchAction).toHaveBeenCalledTimes(1);
  expect(busy()).toBe(false);
  expect(rows()).toHaveLength(1);
  expect(warn).toHaveBeenCalled();
});

it("does not defer an edit or refresh a different train after closing", async () => {
  vi.useFakeTimers();
  let settle!: (ok: boolean) => void;
  vi.mocked(dispatchAction).mockReturnValueOnce(new Promise(resolve => { settle = resolve; }));
  render(data);
  await click("train-timetable-station-2_0_2-add");
  expect(dispatchAction).toHaveBeenCalledTimes(1);
  act(() => tree.unmount());
  await act(async () => { settle(true); vi.advanceTimersByTime(DEFAULT_ACTION_TIMEOUT_MS * 2); });
  expect(dispatchAction).toHaveBeenCalledTimes(1);
});

it("blocks auto-run until an added stop is authoritative and sends toggles immediately", async () => {
  render({ ...data, stops: [] });
  await click("train-timetable-station-1_0_1-add");
  expect(button("train-timetable-auto-run-on").props.disabled).toBe(true);
  publish([a]);
  await click("train-timetable-auto-run-on");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.set_auto_run", { enabled: true });
  act(() => tree.update(createElement(TrainTimetableSection, { timetable: { ...data, isAutoRun: true } })));
  await click("train-timetable-auto-run-off");
  expect(dispatchAction).toHaveBeenLastCalledWith("train_timetable.set_auto_run", { enabled: false });
});

it("disables moves past either end and follows remote changes when idle", () => {
  render(data);
  expect(button("train-timetable-stop-0-up").props.disabled).toBe(true);
  expect(button("train-timetable-stop-0-down").props.disabled).toBe(true);
  publish([a, b]);
  expect(rows()).toHaveLength(2);
  expect(button("train-timetable-stop-0-down").props.disabled).toBeFalsy();
  expect(button("train-timetable-stop-1-down").props.disabled).toBe(true);
  expect(dispatchAction).not.toHaveBeenCalled();
});
