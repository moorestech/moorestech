// タップ/ドラッグ判定を固定する試験
// Pins the pointer-event tap/drag classification, threshold, and drop resolution (precedent: useDragScroll.test.ts)
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { HotbarDragSource } from "./hotbarDnd";

const host = vi.hoisted(() => ({ dispatchAction: vi.fn() }));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return { ...actual, dispatchAction: host.dispatchAction };
});

import { useHotbarDragSource } from "./useHotbarDragSource";

// 5ハンドラをdivへ配線するハーネス
// Test harness wiring the hook's 5 handlers onto a div carrying a data-testid
function Harness({ source, onTap }: { source: HotbarDragSource | null; onTap: () => void }) {
  const handlers = useHotbarDragSource(source, onTap);
  return createElement("div", { "data-testid": "slot", ...handlers });
}

function fakeCurrentTarget() {
  return { setPointerCapture: vi.fn() };
}

// closestのみ持つ疑似要素
// A fake element that only carries closest; used as elementFromPoint's return value
function fakeElementFromPoint(closestResult: unknown) {
  return { closest: () => closestResult };
}

describe("useHotbarDragSource", () => {
  beforeEach(() => {
    host.dispatchAction.mockReset();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("閾値未満で離すとタップとしてonTapが1回呼ばれる(ビルドメニュー/ホットバー枠どちらのクリック選択も壊れない)", () => {
    const onTap = vi.fn();
    const renderer = create(createElement(Harness, { source: { kind: "hotbarSlot", index: 0 }, onTap }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();
    const preventDefault = vi.fn();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault, currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 2, clientY: 2 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 2, clientY: 2 }));

    expect(preventDefault).toHaveBeenCalled();
    expect(ct.setPointerCapture).toHaveBeenCalledWith(1);
    expect(onTap).toHaveBeenCalledTimes(1);
    expect(host.dispatchAction).not.toHaveBeenCalled();
  });

  it("閾値を超えるとドラッグへ確定しonTapは発火しない", () => {
    vi.stubGlobal("document", { elementFromPoint: () => null });
    const onTap = vi.fn();
    const renderer = create(createElement(Harness, { source: { kind: "hotbarSlot", index: 0 }, onTap }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault: vi.fn(), currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 10, clientY: 0 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 10, clientY: 0 }));

    expect(onTap).not.toHaveBeenCalled();
  });

  it("枠外(elementFromPointが枠を返さない)へドラッグして離すとhotbar.clearをdispatchする", () => {
    vi.stubGlobal("document", { elementFromPoint: () => fakeElementFromPoint(null) });
    const renderer = create(createElement(Harness, { source: { kind: "hotbarSlot", index: 3 }, onTap: vi.fn() }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault: vi.fn(), currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 10, clientY: 0 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 10, clientY: 0 }));

    expect(host.dispatchAction).toHaveBeenCalledWith("hotbar.clear", { slot: 3 });
  });

  it("data-hotbar-slot-index付きの枠へドラッグして離すとhotbar.swapをdispatchする", () => {
    const slotElement = { dataset: { hotbarSlotIndex: "5" } };
    vi.stubGlobal("document", { elementFromPoint: () => fakeElementFromPoint(slotElement) });
    const renderer = create(createElement(Harness, { source: { kind: "hotbarSlot", index: 3 }, onTap: vi.fn() }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault: vi.fn(), currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 10, clientY: 0 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 10, clientY: 0 }));

    expect(host.dispatchAction).toHaveBeenCalledWith("hotbar.swap", { from: 3, to: 5 });
  });

  it("ビルドメニューエントリを枠へドラッグして離すとhotbar.assignをdispatchする", () => {
    const slotElement = { dataset: { hotbarSlotIndex: "2" } };
    vi.stubGlobal("document", { elementFromPoint: () => fakeElementFromPoint(slotElement) });
    const renderer = create(createElement(Harness, { source: { kind: "buildMenuEntry", id: "guid-a" }, onTap: vi.fn() }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault: vi.fn(), currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 10, clientY: 0 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 10, clientY: 0 }));

    expect(host.dispatchAction).toHaveBeenCalledWith("hotbar.assign", { slot: 2, id: "guid-a" });
  });

  it("空枠を閾値超えで引きずって離してもタップにならない（建築モードを抜けさせない）", () => {
    vi.stubGlobal("document", { elementFromPoint: () => null });
    const onTap = vi.fn();
    const renderer = create(createElement(Harness, { source: null, onTap }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0, preventDefault: vi.fn(), currentTarget: ct }));
    act(() => view.props.onPointerMove({ pointerId: 1, clientX: 40, clientY: 0 }));
    act(() => view.props.onPointerUp({ pointerId: 1, clientX: 40, clientY: 0 }));

    expect(onTap).not.toHaveBeenCalled();
    expect(host.dispatchAction).not.toHaveBeenCalled();
  });

  it("右ボタン押下は無視し、preventDefaultもpointer捕捉も行わない（右クリック削除の温存）", () => {
    const onTap = vi.fn();
    const renderer = create(createElement(Harness, { source: { kind: "hotbarSlot", index: 0 }, onTap }));
    const view = renderer.root.findByProps({ "data-testid": "slot" });
    const ct = fakeCurrentTarget();
    const preventDefault = vi.fn();

    act(() => view.props.onPointerDown({ isPrimary: true, button: 2, pointerId: 1, clientX: 0, clientY: 0, preventDefault, currentTarget: ct }));

    expect(preventDefault).not.toHaveBeenCalled();
    expect(ct.setPointerCapture).not.toHaveBeenCalled();
  });
});
