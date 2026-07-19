import { createElement, type ReactElement, type ReactNode } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ItemMasterEntry, PlayerInventoryData, ResearchNodeData, ResearchTreeData } from "@/bridge";

const mockState = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  itemMaster: null as Map<number, ItemMasterEntry> | null,
  tree: null as ResearchTreeData | null,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useItemMaster: () => mockState.itemMaster,
    useTopic: (topic: string) => topic === actual.Topics.researchTree ? mockState.tree : mockState.inventory,
  };
});
vi.mock("@/shared/i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("@/shared/treeView", () => ({
  TreeView: (props: object) => createElement("div", { ...props, "data-testid": "mock-tree-view" }),
}));
vi.mock("./ResearchNodeCard", () => ({
  default: (props: object) => createElement("mock-research-node-card", props),
}));
vi.mock("./ResearchDetailPane", () => ({
  default: (props: object) => createElement("mock-research-detail-pane", props),
}));
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: ReactNode }) => createElement("mock-game-panel", null, children),
}));

import ResearchTreePanel from "./ResearchTreePanel";

type TreeViewInstance = ReactTestInstance & {
  props: {
    renderNode: (node: ResearchNodeData, point: { x: number; y: number }) => ReactElement<{
      selected: boolean;
      onSelect: (guid: string) => void;
    }>;
  };
};

const node: ResearchNodeData = {
  guid: "research-a",
  name: "Research A",
  description: "Description",
  state: "researchable",
  iconItemId: 1,
  position: { x: 10, y: 20 },
  prevGuids: [],
  consumeItems: [{ itemId: 1, count: 2 }],
  rewardItems: [],
  unlockItemIds: [],
};

describe("ResearchTreePanel selection toggle", () => {
  beforeEach(() => {
    mockState.tree = { nodes: [node] };
    mockState.inventory = {
      mainSlots: [{ itemId: 1, count: 1 }],
      hotbarSlots: [],
      grab: { itemId: 0, count: 0 },
      selectedHotbar: 0,
    };
    mockState.itemMaster = new Map([[1, { itemId: 1, name: "Iron", maxStack: 100 }]]);
  });

  it("選択トグルで詳細ペインが開閉しrenderNodeが更新される", () => {
    const renderer = create(createElement(ResearchTreePanel));
    const firstTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    const firstRenderNode = firstTree.props.renderNode;
    const card = firstRenderNode(node, node.position);
    expect(card.props.selected).toBe(false);
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);

    // ノード選択で詳細ペインが開く
    // Selecting a node opens the detail pane
    act(() => card.props.onSelect(node.guid));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(1);
    const selectedTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    expect(selectedTree.props.renderNode).not.toBe(firstRenderNode);
    expect(selectedTree.props.renderNode(node, node.position).props.selected).toBe(true);

    // 同ノード再選択で閉じる
    // Re-selecting the same node closes it
    act(() => selectedTree.props.renderNode(node, node.position).props.onSelect(node.guid));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);
  });
});
