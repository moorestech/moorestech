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
  // topic待ちのHUDは解除の後に描かれる。解除時点では透過stageしか無い
  // Topic-gated HUDs render after resume; at resume time only the transparent stage is under the pointer
  const resumeOverTransparentStage = () => {
    hud.dispatchEvent(new MouseEvent("pointermove", { bubbles: true, clientX: 10, clientY: 20 }));
    setSuspended(true);
    const stage = document.createElement("div");
    stage.setAttribute("data-web-ui-transparent", "");
    document.body.appendChild(stage);
    let underPointer: Element = stage;
    Object.defineProperty(document, "elementFromPoint", { configurable: true, value: vi.fn(() => underPointer) });
    bridge.sendInputState.mockClear();
    setSuspended(false);
    expect(bridge.sendInputState).toHaveBeenLastCalledWith(false, false);
    bridge.sendInputState.mockClear();
    return {
      renderHud: () => {
        const hotbar = document.createElement("div");
        stage.appendChild(hotbar);
        underPointer = hotbar;
      },
      stage,
      cleanup: () => stage.remove(),
    };
  };
  const nextFrame = () => new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

  it("解除後に HUD を DOM へ足すと pointermove 無しで (true,false) が送られる", async () => {
    const scene = resumeOverTransparentStage();
    scene.renderHud();
    await nextFrame();
    await nextFrame();

    expect(bridge.sendInputState).toHaveBeenCalledTimes(1);
    expect(bridge.sendInputState).toHaveBeenLastCalledWith(true, false);
    scene.cleanup();
  });

  it("解除後に実 pointermove が来たら DOM 変化で当たり判定し直さない", async () => {
    const scene = resumeOverTransparentStage();
    scene.stage.dispatchEvent(new MouseEvent("pointermove", { bubbles: true, clientX: 300, clientY: 300 }));
    expect(bridge.sendInputState).not.toHaveBeenCalled();
    scene.renderHud();
    await nextFrame();
    await nextFrame();

    expect(bridge.sendInputState).not.toHaveBeenCalled();
    scene.cleanup();
  });

  it("再休止したら DOM 変化で当たり判定し直さない", async () => {
    const scene = resumeOverTransparentStage();
    setSuspended(true);
    scene.renderHud();
    await nextFrame();
    await nextFrame();

    expect(bridge.sendInputState).not.toHaveBeenCalled();
    scene.cleanup();
  });
});
