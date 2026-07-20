import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { useDragScroll } from "./useDragScroll";

// node環境にはDOMが無いため、closestを持つだけの疑似押下要素を使う
// The node test env lacks a DOM, so use a fake press target that only carries closest
const fakeTarget = () => ({ closest: () => null }) as unknown as HTMLElement;

// フックの返す viewportHandlers を data-testid 付き div へ配線するテスト用ハーネス
// Test harness wiring the hook's viewportHandlers onto a div carrying a data-testid
function Harness({ onTap }: { onTap: (target: HTMLElement) => void }) {
  const { viewportHandlers } = useDragScroll({ onTap });
  return createElement("div", { "data-testid": "viewport", ...viewportHandlers });
}

// scrollTop を保持し setPointerCapture をスタブした疑似 viewport 要素
// A fake viewport element that holds scrollTop and stubs setPointerCapture
function fakeViewport(scrollTop: number) {
  return { scrollTop, setPointerCapture: vi.fn() };
}

describe("useDragScroll", () => {
  it("閾値未満で離すとタップとして押下点を通知する", () => {
    const onTap = vi.fn();
    const target = fakeTarget();
    const renderer = create(createElement(Harness, { onTap }));
    const view = renderer.root.findByProps({ "data-testid": "viewport" });
    const vp = fakeViewport(50);

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, target, currentTarget: vp }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 2, clientY: 2, currentTarget: vp }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 2, clientY: 2, currentTarget: vp }));

    // 押下時点で捕捉するので、窓外リリースでもup/cancelが届きジェスチャが残らない
    // Capture at press time so up/cancel always arrive even on an outside release; no gesture leak
    expect(vp.setPointerCapture).toHaveBeenCalledWith(1);
    expect(onTap).toHaveBeenCalledWith(target);
    expect(vp.scrollTop).toBe(50);
  });

  it("閾値を超えて動かすとスクロールし、離してもタップにしない", () => {
    const onTap = vi.fn();
    const renderer = create(createElement(Harness, { onTap }));
    const view = renderer.root.findByProps({ "data-testid": "viewport" });
    const vp = fakeViewport(100);

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 200, target: fakeTarget(), currentTarget: vp }));
    // 上へ30pxドラッグ: scrollTop = 100 - (170 - 200) = 130
    // Drag 30px up: scrollTop = 100 - (170 - 200) = 130
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 0, clientY: 170, currentTarget: vp }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 0, clientY: 170, currentTarget: vp }));

    expect(vp.setPointerCapture).toHaveBeenCalledWith(1);
    expect(vp.scrollTop).toBe(130);
    expect(onTap).not.toHaveBeenCalled();
  });
});
