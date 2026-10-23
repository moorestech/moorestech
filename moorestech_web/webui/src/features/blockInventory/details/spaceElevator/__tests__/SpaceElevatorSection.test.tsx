import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, SpaceElevatorData } from "@/bridge";
import { L } from "@/shared/i18n";

vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, unknown>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/bridge", async (original) => ({
  ...(await original<typeof import("@/bridge")>()),
  dispatchAction: vi.fn().mockResolvedValue({ ok: true }),
}));

import { dispatchAction } from "@/bridge";
import SpaceElevatorSection from "../SpaceElevatorSection";
import { spaceElevatorStatusDisplay } from "../spaceElevatorDisplay";

function renderSection(spaceElevator: SpaceElevatorData | undefined) {
  const data: BlockInventoryOpen = {
    open: true, source: "block", blockType: "SpaceElevatorBase", identifier: "block:20",
    blockGuid: "99999999-9999-4999-8999-999999999999", itemSlots: [], fluidSlots: [], spaceElevator,
  };
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(SpaceElevatorSection, { data })); });
  return tree;
}
const unconnected: SpaceElevatorData = { connected: false, cablePoolAmount: 250, cablesPerElevator: 1000, climbProgress: 0, status: "insufficientCable" };

// サーバー状態を信頼し、残量の比較から接続可否を再導出しない
// Trust the server status instead of deriving connectability again from pool amounts
describe("SpaceElevatorSection", () => {
  it("ケーブル不足なら接続できず不足の理由を示す", () => {
    const tree = renderSection({ ...unconnected, cablePoolAmount: 2000 });
    expect(tree.root.findByType("button").props.disabled).toBe(true);
    expect(tree.root.findAllByProps({ "data-testid": "space-elevator-status" })[0].props.children).toContain(L.ui.blockInventory.spaceElevatorStatusInsufficientCable);
    expect(tree.root.findAllByProps({ "data-insufficient": true }).length).toBeGreaterThan(0);
    act(() => { tree.unmount(); });
  });
  it("接続可能なら空payloadのactionを送る", () => {
    vi.mocked(dispatchAction).mockClear();
    const tree = renderSection({ ...unconnected, status: "readyToConnect" });
    expect(tree.root.findByType("button").props.disabled).toBe(false);
    act(() => { tree.root.findByType("button").props.onClick(); });
    expect(dispatchAction).toHaveBeenCalledTimes(1);
    expect(dispatchAction).toHaveBeenCalledWith("space_elevator.connect", {});
    act(() => { tree.unmount(); });
  });
  it("接続済みなら進捗を描き接続ボタンを隠す", () => {
    const tree = renderSection({ ...unconnected, connected: true, status: "climbing", climbProgress: 0.5 });
    expect(tree.root.findByProps({ role: "progressbar" }).props["aria-valuenow"]).toBe(0.5);
    expect(tree.root.findAllByType("button")).toHaveLength(0);
    act(() => { tree.unmount(); });
  });
  it.each([
    ["readyToConnect", L.ui.blockInventory.spaceElevatorStatusReadyToConnect, false],
    ["insufficientCable", L.ui.blockInventory.spaceElevatorStatusInsufficientCable, true],
    ["waitingSatellite", L.ui.blockInventory.spaceElevatorStatusWaitingSatellite, false],
    ["climbing", L.ui.blockInventory.spaceElevatorStatusClimbing, false],
    ["lowPower", L.ui.blockInventory.spaceElevatorStatusLowPower, true],
    ["outputBlocked", L.ui.blockInventory.spaceElevatorStatusOutputBlocked, true],
  ] as const)("%s の文言と不足色を表示する", (status, labelKey, insufficient) => {
    expect(spaceElevatorStatusDisplay(status)).toEqual({ labelKey, insufficient });
    const tree = renderSection({ ...unconnected, status });
    expect(tree.root.findAllByProps({ "data-testid": "space-elevator-status" })[0].props.children).toContain(labelKey);
    act(() => { tree.unmount(); });
  });
  it("詳細未着では空表示にする", () => {
    const tree = renderSection(undefined);
    expect(tree.toJSON()).toBeNull();
    act(() => { tree.unmount(); });
  });
});
