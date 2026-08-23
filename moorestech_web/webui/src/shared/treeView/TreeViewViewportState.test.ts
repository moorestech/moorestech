import { createElement } from "react";
import { act, create } from "react-test-renderer";
import type { ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// node環境はdocumentを持たないため、CSS変数の読み取りをテスト用の逃げ量へ差し替える
// The node environment has no document, so the CSS variable read is swapped for a test clearance
const CLIP_INSET_PX = 12;
vi.mock("@/shared/tutorialAnchor", () => ({
  readTutorialAnchorClipInsetPx: () => CLIP_INSET_PX,
}));

import TreeView from "./TreeView";

type TestNode = { id: string; x: number; y: number; prevIds: string[] };
type WheelHandler = (event: object) => void;

// テスト用にレイアウト・購読を埋める
// Stub layout values and subscriptions for tests
const wheelHandlers: WheelHandler[] = [];
// offsetは枠線box。中央寄せもズームも内容box(逃げのpaddingを除いた 400x300)基準で解決する
// The offsets are the border box; centering and zoom both resolve against the content box (400x300 minus the clearance)
const createNodeMock = () => ({
  offsetWidth: 400 + CLIP_INSET_PX * 2,
  offsetHeight: 300 + CLIP_INSET_PX * 2,
  getBoundingClientRect: () => ({ width: 400 + CLIP_INSET_PX * 2, left: 0, top: 0 }),
  addEventListener: (type: string, handler: WheelHandler) => {
    if (type === "wheel") wheelHandlers.push(handler);
  },
  removeEventListener: () => undefined,
  setPointerCapture: () => undefined,
});

const baseProps = (props: Partial<Parameters<typeof TreeView<TestNode>>[0]>) => ({
  nodes: [{ id: "node-a", x: 0, y: 0, prevIds: [] }],
  getId: (node: TestNode) => node.id,
  getPosition: (node: TestNode) => ({ x: node.x, y: node.y }),
  getPrevIds: (node: TestNode) => node.prevIds,
  renderNode: () => createElement("span", null, "node"),
  nodeTargetSelector: "[data-node]",
  testIdPrefix: "test",
  ...props,
});

const mount = (props: Partial<Parameters<typeof TreeView<TestNode>>[0]>) => {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(TreeView<TestNode>, baseProps(props)), { createNodeMock });
  });
  return renderer!;
};

const canvasTransform = (renderer: ReactTestRenderer) =>
  renderer.root.findByProps({ "data-testid": "test-canvas" }).props.style.transform as string;

const pointerTarget = { offsetWidth: 100, getBoundingClientRect: () => ({ width: 100 }), setPointerCapture: () => undefined };
const pan = (renderer: ReactTestRenderer, moves: Array<{ x: number; y: number }>) => {
  const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
  act(() => viewport.props.onPointerDown({
    isPrimary: true, button: 0, target: null, pointerId: 1, clientX: 0, clientY: 0, currentTarget: pointerTarget,
  }));
  for (const move of moves) {
    act(() => viewport.props.onPointerMove({ pointerId: 1, clientX: move.x, clientY: move.y, currentTarget: pointerTarget }));
  }
  act(() => viewport.props.onPointerUp({ pointerId: 1 }));
};

// 滑走速度を作る等速ドラッグ(upなし)
// Steady drag building fling velocity (no pointerup)
const dragFast = (renderer: ReactTestRenderer, advance: (ms: number) => void) => {
  const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
  act(() => viewport.props.onPointerDown({
    isPrimary: true, button: 0, target: null, pointerId: 1, clientX: 0, clientY: 0, currentTarget: pointerTarget,
  }));
  for (const clientX of [16, 32, 48, 64]) {
    advance(16);
    act(() => viewport.props.onPointerMove({ pointerId: 1, clientX, clientY: 0, currentTarget: pointerTarget }));
  }
  advance(6);
  return viewport;
};

describe("TreeView viewport state", () => {
  // node環境にはElementが無いためinstanceof判定用に埋める
  // Node env lacks Element, so stub it for the instanceof check
  beforeEach(() => vi.stubGlobal("Element", class TestElement {}));
  afterEach(() => {
    wheelHandlers.length = 0;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("restores the panned viewport after unmount and remount with the same key", () => {
    const first = mount({ viewportKey: "test-persist" });
    pan(first, [{ x: 10, y: 5 }]);
    expect(canvasTransform(first)).toBe("translate(10px, 5px) scale(1)");
    act(() => first.unmount());

    const second = mount({ viewportKey: "test-persist" });
    expect(canvasTransform(second)).toBe("translate(10px, 5px) scale(1)");
  });

  it("centers the initial focus point when nothing is stored", () => {
    // ノード(0,0)を要素中央へ寄せる
    // Centers the node (0,0) in the element
    const renderer = mount({ initialFocus: { x: 0, y: 0 } });
    expect(canvasTransform(renderer)).toBe("translate(0px, -50px) scale(1)");
  });

  it("centers when the focus point arrives after the first data push", () => {
    // サーバー状態未着の初回配信は注目点を持たない（実ゲームの研究topic初回配信と同じ形）
    // The first push carries no focus point, matching the real research topic's state-less first push
    const renderer = mount({ viewportKey: "test-late-focus", initialFocus: null });
    expect(canvasTransform(renderer)).toBe("translate(0px, 0px) scale(1)");

    act(() => renderer.update(createElement(TreeView<TestNode>,
      baseProps({ viewportKey: "test-late-focus", initialFocus: { x: 0, y: 0 } }))));
    expect(canvasTransform(renderer)).toBe("translate(0px, -50px) scale(1)");
  });

  it("prefers the stored viewport over initial-focus centering", () => {
    const first = mount({ viewportKey: "test-stored-wins" });
    pan(first, [{ x: 30, y: 40 }]);
    act(() => first.unmount());

    const second = mount({ viewportKey: "test-stored-wins", initialFocus: { x: 0, y: 0 } });
    expect(canvasTransform(second)).toBe("translate(30px, 40px) scale(1)");
  });

  it("keeps the pre-data zoom scale when centering fires after nodes arrive", () => {
    // 先行ズームはセンタリングで消えない
    // A pre-data zoom survives the centering
    const renderer = mount({ nodes: [], initialFocus: { x: 0, y: 0 } });
    act(() => wheelHandlers[0]({ preventDefault: () => undefined, clientX: 0, clientY: 0, deltaY: -100 }));
    const zoomedScale = Number(/scale\(([\d.]+)\)/.exec(canvasTransform(renderer))![1]);
    expect(zoomedScale).toBeGreaterThan(1);

    act(() => renderer.update(createElement(TreeView<TestNode>, baseProps({ initialFocus: { x: 0, y: 0 } }))));
    const centeredScale = Number(/scale\(([\d.]+)\)/.exec(canvasTransform(renderer))![1]);
    expect(centeredScale).toBeCloseTo(zoomedScale, 5);
  });

  // クリップ境界を広げたぶんキャンバス原点はpadding分ずれる。基準を内容boxへ揃えないとズームが逃げの分だけ流れる
  // Widening the clip edge offsets the canvas origin by the padding; without content-box anchoring the zoom drifts by the clearance
  it("anchors wheel zoom at the content box, so the clearance does not shift the view", () => {
    const renderer = mount({});
    // 内容box原点(=枠線box + 逃げ)を指したホイールは、その点を動かさずに倍率だけを変える
    // A wheel over the content-box origin (border box + clearance) changes only the scale and keeps that point put
    act(() => wheelHandlers[0]({ preventDefault: () => undefined, clientX: CLIP_INSET_PX, clientY: CLIP_INSET_PX, deltaY: -100 }));
    expect(canvasTransform(renderer)).toMatch(/^translate\(0px, 0px\) scale\(1\.[\d]+\)$/);
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
    const viewport = dragFast(renderer, (ms) => { now += ms; });
    act(() => viewport.props.onPointerUp({ pointerId: 1 }));
    expect(frameQueue.length).toBe(1);

    // rAFで滑走し減衰しきって停止
    // rAF glides, decays, then stops
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

  it("aborts without gliding when the pointer is cancelled", () => {
    let now = 0;
    vi.spyOn(performance, "now").mockImplementation(() => now);
    const frameQueue: FrameRequestCallback[] = [];
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      frameQueue.push(callback);
      return frameQueue.length;
    });
    vi.stubGlobal("cancelAnimationFrame", () => undefined);

    const renderer = mount({});
    const viewport = dragFast(renderer, (ms) => { now += ms; });
    act(() => viewport.props.onPointerCancel({ pointerId: 1 }));
    expect(frameQueue.length).toBe(0);
    expect(canvasTransform(renderer)).toBe("translate(64px, 0px) scale(1)");
  });
});
