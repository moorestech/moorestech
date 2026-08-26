import { createElement, type ReactElement, type ReactNode } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PlayerInventoryData, ResearchNodeData, ResearchTreeData } from "@/bridge";

const mockState = vi.hoisted(() => ({
  inventory: null as PlayerInventoryData | null,
  tree: null as ResearchTreeData | null,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => topic === actual.Topics.researchTree ? mockState.tree : mockState.inventory,
  };
});
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
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
    renderNode: (node: ResearchNodeData, point: { x: number; y: number }) => ReactElement<{ selected: boolean }>;
    // 選択はTreeViewのタップ判定から届く(ADR 0033)
    // Selection arrives from TreeView's tap detection (ADR 0033)
    onNodeTap: (node: ResearchNodeData) => void;
  };
};

const node: ResearchNodeData = {
  guid: "86000000-0000-4000-8000-000000000001",
  state: "researchable",
  iconItemId: 1,
  position: { x: 10, y: 20 },
  prevGuids: [],
  consumeItems: [{ itemId: 1, count: 2 }],
  rewardItems: [],
  unlockItemRecipeViewItemIds: [],
  unlockBlocks: [],
  unlockMachineRecipes: [],
  unlockConnectToolGuids: [],
  unlockTrainCarGuids: [],
};

describe("ResearchTreePanel selection toggle", () => {
  beforeEach(() => {
    mockState.tree = { nodes: [node] };
    mockState.inventory = {
      mainSlots: [{ itemId: 1, count: 1 }],
      grab: { itemId: 0, count: 0 },
      equipment: [],
      selectedEquipment: -1,
      equipmentSelectionConfirmationRevision: 0,
    };
  });

  it("選択トグルで詳細ペインが開閉しrenderNodeが更新される", () => {
    const renderer = create(createElement(ResearchTreePanel));
    const firstTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    const firstRenderNode = firstTree.props.renderNode;
    const card = firstRenderNode(node, node.position);
    expect(card.props.selected).toBe(false);
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);

    // ノードのタップで詳細ペインが開く
    // Tapping a node opens the detail pane
    act(() => firstTree.props.onNodeTap(node));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(1);
    const selectedTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    expect(selectedTree.props.renderNode).not.toBe(firstRenderNode);
    expect(selectedTree.props.renderNode(node, node.position).props.selected).toBe(true);

    // 同ノード再タップで閉じる
    // Tapping the same node again closes it
    act(() => selectedTree.props.onNodeTap(node));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);
  });

  it("インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4)", () => {
    mockState.inventory = null;
    const renderer = create(createElement(ResearchTreePanel));
    const tree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    act(() => tree.props.onNodeTap(node));
    const pane = renderer.root.findByType("mock-research-detail-pane" as never);
    expect((pane.props as unknown as { owned: Map<number, number> | null }).owned).toBeNull();
  });
});
