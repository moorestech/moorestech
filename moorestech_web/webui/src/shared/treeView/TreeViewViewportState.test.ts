import { createElement } from "react";
import { act, create } from "react-test-renderer";
import type { ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import TreeView from "./TreeView";

type TestNode = { id: string; x: number; y: number; prevIds: string[] };

// ビューポート要素のレイアウト値・イベント購読をテスト用に埋める
// Provide layout values and event subscription stubs for the viewport element
const createNodeMock = () => ({
  offsetWidth: 400,
  offsetHeight: 300,
  getBoundingClientRect: () => ({ width: 400, left: 0, top: 0 }),
  addEventListener: () => undefined,
  removeEventListener: () => undefined,
  setPointerCapture: () => undefined,
});

const mount = (props: Partial<Parameters<typeof TreeView<TestNode>>[0]>) => {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(TreeView<TestNode>, {
      nodes: [{ id: "node-a", x: 0, y: 0, prevIds: [] }],
      getId: (node: TestNode) => node.id,
      getPosition: (node: TestNode) => ({ x: node.x, y: node.y }),
      getPrevIds: (node: TestNode) => node.prevIds,
      renderNode: () => createElement("span", null, "node"),
      nodeTargetSelector: "[data-node]",
      testIdPrefix: "test",
      ...props,
    }), { createNodeMock });
  });
  return renderer!;
};

const canvasTransform = (renderer: ReactTestRenderer) =>
  renderer.root.findByProps({ "data-testid": "test-canvas" }).props.style.transform as string;

const pointerTarget = { offsetWidth: 100, getBoundingClientRect: () => ({ width: 100 }), setPointerCapture: () => undefined };
const pan = (renderer: ReactTestRenderer, moves: Array<{ x: number; y: number }>, endWithUp: boolean) => {
  const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
  act(() => viewport.props.onPointerDown({
    isPrimary: true, button: 0, target: null, pointerId: 1, clientX: 0, clientY: 0, currentTarget: pointerTarget,
  }));
  for (const move of moves) {
    act(() => viewport.props.onPointerMove({ pointerId: 1, clientX: move.x, clientY: move.y, currentTarget: pointerTarget }));
  }
  if (endWithUp) act(() => viewport.props.onPointerUp({ pointerId: 1 }));
};

describe("TreeView viewport state", () => {
  // node環境にはElementが無いためinstanceof判定用に埋める
  // Node env lacks Element, so stub it for the instanceof check
  beforeEach(() => vi.stubGlobal("Element", class TestElement {}));
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("restores the panned viewport after unmount and remount with the same key", () => {
    const first = mount({ viewportKey: "test-persist" });
    pan(first, [{ x: 10, y: 5 }], true);
    expect(canvasTransform(first)).toBe("translate(10px, 5px) scale(1)");
    act(() => first.unmount());

    const second = mount({ viewportKey: "test-persist" });
    expect(canvasTransform(second)).toBe("translate(10px, 5px) scale(1)");
  });

  it("centers the initial focus point when nothing is stored", () => {
    // 単一ノード(0,0)はキャンバス(200,200)。400x300要素の中央(200,150)へ寄せる
    // The single node at (0,0) maps to canvas (200,200); centered in the 400x300 element (200,150)
    const renderer = mount({ initialFocus: { x: 0, y: 0 } });
    expect(canvasTransform(renderer)).toBe("translate(0px, -50px) scale(1)");
  });

  it("prefers the stored viewport over initial-focus centering", () => {
    const first = mount({ viewportKey: "test-stored-wins" });
    pan(first, [{ x: 30, y: 40 }], true);
    act(() => first.unmount());

    const second = mount({ viewportKey: "test-stored-wins", initialFocus: { x: 0, y: 0 } });
    expect(canvasTransform(second)).toBe("translate(30px, 40px) scale(1)");
  });

  it("keeps gliding after a fast drag is released and eventually stops", () => {
    let now = 0;
    vi.spyOn(performance, "now").mockImplementation(() => now);
    const frameQueue: FrameRequestCallback[] = [];
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      frameQueue.push(callback);
      return frameQueue.length;
    });
    vi.stubGlobal("cancelAnimationFrame", () => undefined);

    const renderer = mount({});
    const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
    act(() => viewport.props.onPointerDown({
      isPrimary: true, button: 0, target: null, pointerId: 1, clientX: 0, clientY: 0, currentTarget: pointerTarget,
    }));
    // 16ms間隔で等速ドラッグして速度を作り、直後に離す
    // Build velocity with steady 16ms-interval drag moves, then release immediately
    for (const clientX of [16, 32, 48, 64]) {
      now += 16;
      act(() => viewport.props.onPointerMove({ pointerId: 1, clientX, clientY: 0, currentTarget: pointerTarget }));
    }
    now += 6;
    act(() => viewport.props.onPointerUp({ pointerId: 1 }));
    expect(frameQueue.length).toBe(1);

    // rAFを進めると離した位置(64px)を越えて滑走し、減衰しきって停止する
    // Advancing rAF glides past the release position (64px), decays, and stops
    let frames = 0;
    while (frameQueue.length > 0 && frames < 500) {
      const callback = frameQueue.shift()!;
      now += 16;
      act(() => callback(now));
      frames++;
    }
    expect(frames).toBeLessThan(500);
    const finalX = Number(/translate\((-?[\d.]+)px/.exec(canvasTransform(renderer))![1]);
    expect(finalX).toBeGreaterThan(70);
    expect(finalX).toBeLessThan(64 + 300);
  });
});
