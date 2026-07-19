import { createElement, type ReactNode } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ItemMasterEntry, PlayerInventoryData, ResearchNodeData, ResearchTreeData } from "@/bridge";

const mockState = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  itemMaster: null as Map<number, ItemMasterEntry> | null,
  tree: null as ResearchTreeData | null,
}));

vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: ReactNode }) => createElement("mock-box", props, children),
  Title: ({ children, ...props }: { children: ReactNode }) => createElement("mock-title", props, children),
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

import ResearchTreePanel from "./ResearchTreePanel";

type TreeViewInstance = ReactTestInstance & {
  props: {
    renderNode: (node: ResearchNodeData, point: { x: number; y: number }) => ReactTestInstance;
  };
};

const node: ResearchNodeData = {
  guid: "research-a",
  name: "Research A",
  description: "Description",
  state: "researchable",
  position: { x: 10, y: 20 },
  prevGuids: [],
  consumeItems: [{ itemId: 1, count: 2 }],
  rewardItems: [],
  unlockItemIds: [],
};

describe("ResearchTreePanel render cache inputs", () => {
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

  it("rebuilds research cards after inventory or item-master updates", () => {
    const renderer = create(createElement(ResearchTreePanel));
    const firstTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    const firstRenderNode = firstTree.props.renderNode;
    const firstCard = firstRenderNode(node, node.position);
    expect(firstCard.props.owned.get(1)).toBe(1);
    expect(firstCard.props.resolveName(1)).toBe("Iron");

    // 所持数更新で描画関数を更新する
    // Replace the render function after inventory updates
    mockState.inventory = { ...mockState.inventory!, mainSlots: [{ itemId: 1, count: 5 }] };
    act(() => renderer.update(createElement(ResearchTreePanel)));
    const inventoryTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    expect(inventoryTree.props.renderNode).not.toBe(firstRenderNode);
    expect(inventoryTree.props.renderNode(node, node.position).props.owned.get(1)).toBe(5);

    // マスタ更新で名称解決を更新する
    // Refresh name resolution after master updates
    const inventoryRenderNode = inventoryTree.props.renderNode;
    mockState.itemMaster = new Map([[1, { itemId: 1, name: "Steel", maxStack: 100 }]]);
    act(() => renderer.update(createElement(ResearchTreePanel)));
    const masterTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    expect(masterTree.props.renderNode).not.toBe(inventoryRenderNode);
    expect(masterTree.props.renderNode(node, node.position).props.resolveName(1)).toBe("Steel");
  });
});
