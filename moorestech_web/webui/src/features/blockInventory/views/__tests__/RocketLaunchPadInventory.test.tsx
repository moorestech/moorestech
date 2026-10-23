import { createElement } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
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
import { GamePanel } from "@/shared/ui";

function hostById(root: ReactTestInstance, id: string) {
  return root.findAll((node) => typeof node.type === "string" && node.props["data-testid"] === id)[0];
}
function frameIn(root: ReactTestInstance, group: string) {
  return hostById(root, `rocket-launch-pad-${group}-slots`).findAll((node) => node.type === "div" && node.props.onMouseDown)[0];
}

describe("RocketLaunchPadInventory through the real panel", () => {
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
