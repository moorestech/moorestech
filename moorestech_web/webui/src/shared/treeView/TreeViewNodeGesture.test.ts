import { createElement } from "react";
import { act, create } from "react-test-renderer";
import type { ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";

// node環境はdocumentを持たないため、CSS変数の読み取りをテスト用の逃げ量へ差し替える
// The node environment has no document, so the CSS variable read is swapped for a test clearance
vi.mock("@/shared/tutorialAnchor", () => ({ readTutorialAnchorClipInsetPx: () => 0 }));

import TreeView from "./TreeView";
import { NODE_ID_ATTRIBUTE } from "./useTreePanGesture";

type TestNode = { id: string; x: number; y: number; prevIds: string[] };

const nodes: TestNode[] = [{ id: "node-a", x: 0, y: 0, prevIds: [] }];
const pointerTarget = { offsetWidth: 100, getBoundingClientRect: () => ({ width: 100 }), setPointerCapture: () => undefined };

// 実際に描かれた包みから印を読む。TreeViewが印を出さなくなればここで落ちる
// Read the mark off the wrapper TreeView actually rendered, so dropping the mark fails here
const markedNodeId = (renderer: ReactTestRenderer) => {
  const wrappers = renderer.root.findAllByProps({ [NODE_ID_ATTRIBUTE]: nodes[0].id });
  expect(wrappers).toHaveLength(1);
  return wrappers[0].props[NODE_ID_ATTRIBUTE] as string;
};

// ノードの印を持つ押下点のモック
// A mock press point carrying a node's mark
const pressOnNode = (id: string) => ({
  closest: (selector: string) => (selector === `[${NODE_ID_ATTRIBUTE}]`
    ? { getAttribute: (name: string) => (name === NODE_ID_ATTRIBUTE ? id : null) }
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
const press = (viewport: Viewport, target: object | null, pointerId = 1) => act(() => viewport.props.onPointerDown({
  isPrimary: true, button: 0, target, pointerId, clientX: 0, clientY: 0, currentTarget: pointerTarget,
}));
const move = (viewport: Viewport, x: number, y: number) => act(() => viewport.props.onPointerMove({
  pointerId: 1, clientX: x, clientY: y, currentTarget: pointerTarget,
}));
const release = (viewport: Viewport, button = 0, pointerId = 1) =>
  act(() => viewport.props.onPointerUp({ pointerId, button }));

describe("TreeView node gesture", () => {
  afterEach(() => vi.restoreAllMocks());

  it("selects the pressed node when the release stays under the drag threshold", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    // 4px移動は閾値未満でタップのまま
    // A 4px move stays under the threshold and remains a tap
    move(viewport, 3, 2);
    release(viewport);
    expect(onNodeTap).toHaveBeenCalledWith(nodes[0]);
  });

  it("keeps the canvas still until the threshold is exceeded", () => {
    const renderer = mount(vi.fn());
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    move(viewport, 3, 2);
    expect(canvasTransformOf(renderer)).toBe("translate(0px, 0px) scale(1)");
  });

  it("pans from a node press and never selects once it becomes a drag", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    // 閾値超えは押下点からの全量が動く
    // The move crossing the threshold pans the whole delta from the press point
    move(viewport, 20, 10);
    expect(canvasTransformOf(renderer)).toBe("translate(20px, 10px) scale(1)");
    release(viewport);
    expect(onNodeTap).not.toHaveBeenCalled();
  });

  it("taps nothing when the press point is outside every node", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, { closest: () => null });
    release(viewport);
    expect(onNodeTap).not.toHaveBeenCalled();
  });

  it("treats a cancelled press as an aborted gesture, not a tap", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    act(() => viewport.props.onPointerCancel({ pointerId: 1 }));
    expect(onNodeTap).not.toHaveBeenCalled();
  });

  it("ignores a secondary button release and keeps the press alive", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    release(viewport, 2);
    expect(onNodeTap).not.toHaveBeenCalled();
    release(viewport);
    expect(onNodeTap).toHaveBeenCalledWith(nodes[0]);
  });

  it("lets no second pointer hijack the live press", () => {
    const onNodeTap = vi.fn();
    const renderer = mount(onNodeTap);
    const viewport = viewportOf(renderer);
    press(viewport, pressOnNode(markedNodeId(renderer)));
    press(viewport, { closest: () => null }, 2);
    // 乗っ取られていなければ第2ポインタの解放は無視される
    // With no hijack, the second pointer's release is ignored
    release(viewport, 0, 2);
    expect(onNodeTap).not.toHaveBeenCalled();
    release(viewport);
    expect(onNodeTap).toHaveBeenCalledWith(nodes[0]);
  });
});
