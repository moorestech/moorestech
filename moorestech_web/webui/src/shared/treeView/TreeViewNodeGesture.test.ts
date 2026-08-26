import { createElement } from "react";
import { act, create } from "react-test-renderer";
import type { ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";

// node環境はdocumentを持たないため、CSS変数の読み取りをテスト用の逃げ量へ差し替える
// The node environment has no document, so the CSS variable read is swapped for a test clearance
vi.mock("@/shared/tutorialAnchor", () => ({ readTutorialAnchorClipInsetPx: () => 0 }));

import TreeView from "./TreeView";

type TestNode = { id: string; x: number; y: number; prevIds: string[] };

const nodes: TestNode[] = [{ id: "node-a", x: 0, y: 0, prevIds: [] }];
const pointerTarget = { offsetWidth: 100, getBoundingClientRect: () => ({ width: 100 }), setPointerCapture: () => undefined };

// ノード包みの印を持つ押下点。TreeViewはこの祖先を辿ってタップ対象を引く
// A press point carrying the wrapper mark; TreeView walks up to it to resolve the tap target
const pressOnNode = (id: string) => ({
  closest: (selector: string) => (selector === "[data-tree-node-id]"
    ? { getAttribute: (name: string) => (name === "data-tree-node-id" ? id : null) }
    : null),
});

const mount = (onNodeTap: (node: TestNode) => void) => {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(TreeView<TestNode>, {
      nodes,
      getId: (node: TestNode) => node.id,
      getPosition: (node: TestNode) => ({ x: node.x, y: node.y }),
      getPrevIds: (node: TestNode) => node.prevIds,
      renderNode: () => createElement("span", null, "node"),
      onNodeTap,
      testIdPrefix: "test",
    }), {
      createNodeMock: () => ({
        offsetWidth: 400, offsetHeight: 300,
        getBoundingClientRect: () => ({ width: 400, left: 0, top: 0 }),
        addEventListener: () => undefined, removeEventListener: () => undefined,
        setPointerCapture: () => undefined,
      }),
    });
  });
  return renderer!;
};

const viewportOf = (renderer: ReactTestRenderer) => renderer.root.findByProps({ "data-testid": "test-viewport" });
const canvasTransformOf = (renderer: ReactTestRenderer) =>
  renderer.root.findByProps({ "data-testid": "test-canvas" }).props.style.transform as string;

type Viewport = ReturnType<typeof viewportOf>;
const press = (viewport: Viewport, target: object | null) => act(() => viewport.props.onPointerDown({
  isPrimary: true, button: 0, target, pointerId: 1, clientX: 0, clientY: 0, currentTarget: pointerTarget,
}));
const move = (viewport: Viewport, x: number, y: number) => act(() => viewport.props.onPointerMove({
  pointerId: 1, clientX: x, clientY: y, currentTarget: pointerTarget,
}));

describe("TreeView node gesture", () => {
  afterEach(() => vi.restoreAllMocks());

  it("selects the pressed node when the release stays under the drag threshold", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode("node-a"));
    // 4pxの手ぶれは閾値未満なのでタップのまま
    // A 4px tremor stays under the threshold and remains a tap
    move(viewport, 3, 2);
    act(() => viewport.props.onPointerUp({ pointerId: 1 }));
    expect(onNodeTap).toHaveBeenCalledWith(nodes[0]);
  });

  it("keeps the canvas still until the threshold is exceeded", () => {
    const renderer = mount(vi.fn());
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode("node-a"));
    move(viewport, 3, 2);
    expect(canvasTransformOf(renderer)).toBe("translate(0px, 0px) scale(1)");
  });

  it("pans from a node press and never selects once it becomes a drag", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode("node-a"));
    // 閾値超えの回は押下点からの全量ぶん動き、内容がポインタへ追従する
    // The move that crosses the threshold pans the whole delta from the press point, keeping content under the pointer
    move(viewport, 20, 10);
    expect(canvasTransformOf(renderer)).toBe("translate(20px, 10px) scale(1)");
    act(() => viewport.props.onPointerUp({ pointerId: 1 }));
    expect(onNodeTap).not.toHaveBeenCalled();
  });

  it("taps nothing when the press point is outside every node", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, { closest: () => null });
    act(() => viewport.props.onPointerUp({ pointerId: 1 }));
    expect(onNodeTap).not.toHaveBeenCalled();
  });

  it("treats a cancelled press as an aborted gesture, not a tap", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode("node-a"));
    act(() => viewport.props.onPointerCancel({ pointerId: 1 }));
    expect(onNodeTap).not.toHaveBeenCalled();
  });
});
