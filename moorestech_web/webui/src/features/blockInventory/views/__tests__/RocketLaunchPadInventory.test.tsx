import { createElement } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { beforeEach, describe, expect, expectTypeOf, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";
import { loadFixture } from "../../../../bridge/contract/wireFixtures.test-helper";

const mock = vi.hoisted(() => ({ dispatch: vi.fn(), block: {} as BlockInventoryOpen, grab: { itemId: 0, count: 0 } }));
// 実パネル・スロットで外部状態を固定
// Keep real panels and slots while fixing external topic state
vi.mock("@/bridge", async (original) => ({
  ...(await original<typeof import("@/bridge")>()),
  dispatchAction: mock.dispatch,
  useConnectionStatus: () => "connected",
  useTopic: (topic: string) => topic === "block_inventory.current" ? mock.block : undefined,
  readTopic: (topic: string) => topic === "block_inventory.current" ? mock.block : { grab: mock.grab, mainSlots: [] },
}));
vi.mock("@/shared/i18n", async (original) => ({
  ...(await original<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Tooltip: ({ children }: { children: unknown }) => children,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("section", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("main", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("p", props, children as never),
}));
import BlockInventoryPanel from "../../BlockInventoryPanel";
import RocketLaunchPadInventory from "../RocketLaunchPadInventory";
import { GamePanel } from "@/shared/ui";

function hostById(root: ReactTestInstance, id: string) {
  return root.findAll((node) => typeof node.type === "string" && node.props["data-testid"] === id)[0];
}
function frameIn(root: ReactTestInstance, group: string) {
  return hostById(root, `rocket-launch-pad-${group}-slots`).findAll((node) => node.type === "div" && node.props.onMouseDown)[0];
}

describe("RocketLaunchPadInventory through the real panel", () => {
  it("accepts panel data and narrows inside the dedicated view", () => {
    expectTypeOf<BlockInventoryOpen>().toEqualTypeOf<Parameters<typeof RocketLaunchPadInventory>[0]["data"]>();
  });

  it("a mismatched registry key shows pending detail without throwing", () => {
    mock.block = { ...mock.block, blockType: "Chest" };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(RocketLaunchPadInventory, { data: mock.block })); });
    expect(hostById(tree.root, "rocket-launch-pad-loading")).toBeDefined();
    expect(hostById(tree.root, "rocket-launch-pad-view")).toBeUndefined();
    act(() => { tree.unmount(); });
  });

  it("waits for initial detail and renders when the topic arrives", () => {
    const detail = mock.block.rocketLaunchPad;
    mock.block = { ...mock.block, rocketLaunchPad: undefined };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(BlockInventoryPanel)); });
    expect(hostById(tree.root, "rocket-launch-pad-view")).toBeUndefined();
    expect(hostById(tree.root, "rocket-launch-pad-loading").props.children).toBe("ui.blockInventory.detailLoading");
    mock.block = { ...mock.block, rocketLaunchPad: detail };
    act(() => { tree.update(createElement(BlockInventoryPanel)); });
    expect(hostById(tree.root, "rocket-launch-pad-view")).toBeDefined();
    expect(hostById(tree.root, "rocket-launch-pad-loading")).toBeUndefined();
    act(() => { tree.unmount(); });
  });

  beforeEach(() => {
    mock.block = loadFixture("block_inventory_rocket_launch_pad.json") as BlockInventoryOpen;
    mock.grab = { itemId: 0, count: 0 };
    mock.dispatch.mockReset();
  });

  it("dispatches both commands from GamePanel.titleAction and hides them outside command waits", () => {
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(BlockInventoryPanel)); });
    expect(tree.root.findByType(GamePanel).props.titleAction).toBeDefined();
    expect(hostById(tree.root, "rocket-launch-pad-view")).toBeDefined();
    expect(hostById(tree.root, "rocket-launch-pad-launch")).toBeUndefined();
    act(() => { hostById(tree.root, "rocket-launch-pad-build").props.onClick(); });
    expect(mock.dispatch).toHaveBeenLastCalledWith("rocket_launch_pad.command", { command: "build" });

    // topicが変わるまでボタン・表示を楽観更新しない
    // Keep the button and display unchanged until the topic changes
    expect(hostById(tree.root, "rocket-launch-pad-build")).toBeDefined();
    for (const phase of ["awaitingLaunchCommand", "idle", "building", "readyToLaunch", "launching"] as const) {
      mock.block = { ...mock.block, rocketLaunchPad: { ...mock.block.rocketLaunchPad!, phase } };
      act(() => { tree.update(createElement(BlockInventoryPanel)); });
      expect(hostById(tree.root, "rocket-launch-pad-build")).toBeUndefined();
      if (phase === "awaitingLaunchCommand") {
        act(() => { hostById(tree.root, "rocket-launch-pad-launch").props.onClick(); });
        expect(mock.dispatch).toHaveBeenLastCalledWith("rocket_launch_pad.command", { command: "launch" });
      } else expect(hostById(tree.root, "rocket-launch-pad-launch")).toBeUndefined();
    }
    act(() => { tree.unmount(); });
  });

  it.each([
    ["build", "awaitingBuildCommand", "building", "rocket_command_not_awaiting_build"],
    ["launch", "awaitingLaunchCommand", "readyToLaunch", "rocket_command_not_awaiting_launch"],
  ] as const)("%sの成功後topicが遅れても二度目の拒否を誤報しない", async (command, phase, nextPhase, error) => {
    vi.resetModules();
    const actual = await vi.importActual<typeof import("@/bridge")>("@/bridge");
    const notify = vi.fn();
    const sends: string[] = [];
    class SocketStub {
      static readonly OPEN = 1;
      readyState = 1;
      onmessage: ((event: { data: string }) => void) | null = null;
      send(raw: string) {
        const request = JSON.parse(raw);
        if (request.op !== "action") return;
        sends.push(raw);
        queueMicrotask(() => this.onmessage?.({ data: JSON.stringify({ op: "result", requestId: request.requestId, ok: sends.length === 1, error }) }));
      }
    }
    vi.stubGlobal("WebSocket", SocketStub);
    vi.stubGlobal("window", globalThis);
    vi.stubGlobal("location", { protocol: "http:", host: "example.test" });
    actual.setToastSink(notify);
    actual.initBridge();
    mock.dispatch.mockImplementation(actual.dispatchAction);
    mock.block = { ...mock.block, rocketLaunchPad: { ...mock.block.rocketLaunchPad!, phase } };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(BlockInventoryPanel)); });
    await act(async () => { hostById(tree.root, `rocket-launch-pad-${command}`).props.onClick(); });
    // 応答後にもtopicは古いままにして再操作する
    // Click again after the response while the topic still carries the old phase
    await act(async () => { hostById(tree.root, `rocket-launch-pad-${command}`).props.onClick(); });
    expect(sends).toHaveLength(2);
    expect(notify).not.toHaveBeenCalled();
    mock.block = { ...mock.block, rocketLaunchPad: { ...mock.block.rocketLaunchPad!, phase: nextPhase } };
    act(() => { tree.update(createElement(BlockInventoryPanel)); });
    expect(hostById(tree.root, `rocket-launch-pad-${command}`)).toBeUndefined();
    act(() => { tree.unmount(); });
    actual.setToastSink(() => {});
    vi.unstubAllGlobals();
  });

  it("renders DTO groups and preserves their absolute indices through real mouse gestures", () => {
    // 意図的に並びを変え、Webが0から振り直す故障を検出する
    // Deliberately reorder indices to detect the Web renumbering slots from zero
    mock.block = {
      ...mock.block,
      itemSlots: [{ itemId: 3, count: 20 }, { itemId: 4, count: 5 }, { itemId: 5, count: 7 }],
      rocketLaunchPad: { ...mock.block.rocketLaunchPad!, materialSlots: [2], payloadSlots: [0], outputSlots: [1], progress: 0.375, waitReason: "outputFull" },
    };
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(BlockInventoryPanel)); });
    expect(hostById(tree.root, "rocket-launch-pad-progress").props["aria-valuenow"]).toBe(0.375);
    expect(hostById(tree.root, "rocket-launch-pad-status").props.children).toBe("ui.blockInventory.rocketWaitOutputFull");
    for (const [group, index] of [["material", 2], ["payload", 0], ["output", 1]] as const) {
      const slot = frameIn(tree.root, group);
      expect(slot.findByType("span").props.children).toBe(String(mock.block.itemSlots[index].count));
      act(() => { slot.props.onMouseDown({ button: 0, shiftKey: false, preventDefault: vi.fn() }); });
      expect(mock.dispatch).toHaveBeenLastCalledWith("block_inventory.move_item", {
        from: { area: "block", slot: index }, to: { area: "grab", slot: 0 }, count: mock.block.itemSlots[index].count,
      });
      act(() => { slot.props.onMouseDown({ button: 2, preventDefault: vi.fn() }); });
      expect(mock.dispatch).toHaveBeenLastCalledWith("block_inventory.split", { from: { area: "block", slot: index } });
      act(() => { slot.props.onDoubleClick(); });
      expect(mock.dispatch).toHaveBeenLastCalledWith("block_inventory.collect", { slot: { area: "block", slot: index } });
    }
    // 保持中の投入も同じ絶対添字へ送る
    // Placement from the held stack targets the same absolute index
    mock.grab = { itemId: 3, count: 9 };
    act(() => { frameIn(tree.root, "payload").props.onMouseDown({ button: 0, shiftKey: false, preventDefault: vi.fn() }); });
    expect(mock.dispatch).toHaveBeenLastCalledWith("block_inventory.move_item", { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 0 }, count: 9 });
    act(() => { tree.unmount(); });
  });
});
