// @vitest-environment jsdom
// 休止（R1）解除時に送信済みキャッシュを捨て、ポインタを動かさなくても実状態をUnityへ送り直すことを検証
// Verifies that resuming from suspension (R1) drops the sent cache and resends the real state without any pointer move
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const bridge = vi.hoisted(() => ({ sendInputState: vi.fn() }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  sendInputState: bridge.sendInputState,
}));

import { useWebInputExclusivity } from "./useWebInputExclusivity";

function Probe({ suspended }: { suspended: boolean }) {
  useWebInputExclusivity(suspended);
  return null;
}

describe("useWebInputExclusivity の休止解除（R1）", () => {
  let renderer!: ReactTestRenderer;
  let hud: HTMLDivElement;

  beforeEach(() => {
    bridge.sendInputState.mockClear();
    hud = document.createElement("div");
    document.body.appendChild(hud);
    act(() => { renderer = create(createElement(Probe, { suspended: false })); });
  });

  afterEach(() => {
    act(() => renderer.unmount());
    hud.remove();
    Reflect.deleteProperty(document, "elementFromPoint");
  });

  const setSuspended = (suspended: boolean) => act(() => renderer.update(createElement(Probe, { suspended })));

  it("HUD上でマウスを動かさずに解除しても pointerOverUi=true が届く", () => {
    hud.dispatchEvent(new MouseEvent("pointermove", { bubbles: true, clientX: 10, clientY: 20 }));
    expect(bridge.sendInputState).toHaveBeenLastCalledWith(true, false);

    // 休止中もキャッシュはtrueのまま。解除で同じ位置を当たり判定し直す
    // The cache still holds true while suspended; resuming re-hit-tests the same position
    setSuspended(true);
    const elementFromPoint = vi.fn(() => hud);
    Object.defineProperty(document, "elementFromPoint", { configurable: true, value: elementFromPoint });
    bridge.sendInputState.mockClear();
    setSuspended(false);

    expect(elementFromPoint).toHaveBeenCalledWith(10, 20);
    expect(bridge.sendInputState).toHaveBeenCalledTimes(1);
    expect(bridge.sendInputState).toHaveBeenLastCalledWith(true, false);
  });

  it("ポインタ位置を一度も受けていなければ解除時に false を送る", () => {
    setSuspended(true);
    bridge.sendInputState.mockClear();
    setSuspended(false);

    expect(bridge.sendInputState).toHaveBeenCalledTimes(1);
    expect(bridge.sendInputState).toHaveBeenLastCalledWith(false, false);
  });

  it("解除時にフォーカス中の文字入力欄があれば textInputFocused=true を送る", () => {
    const input = document.createElement("input");
    input.type = "text";
    document.body.appendChild(input);
    setSuspended(true);
    input.focus();
    bridge.sendInputState.mockClear();
    setSuspended(false);

    expect(bridge.sendInputState).toHaveBeenLastCalledWith(false, true);
    input.remove();
  });
});
