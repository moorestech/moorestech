// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useTopicStore } from "../store/topicStore";
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { resolveTutorialAnchor } from "../../shared/tutorialAnchor/resolveAnchor";
import { initDomQueryResponder } from "./domQueryResponder";

vi.mock("../transport/webSocketClient", () => ({ sendAction: vi.fn().mockResolvedValue({ ok: true }) }));
vi.mock("../transport/subscriptionManager", () => ({ subscriptions: { acquire: vi.fn(), release: vi.fn() } }));

let dispose: () => void;
let element: HTMLElement;

beforeEach(() => {
  vi.mocked(sendAction).mockClear().mockResolvedValue({ ok: true });
  vi.stubGlobal("CSS", { escape: (value: string) => value });
  useTopicStore.setState({ topics: {}, status: "open" });
  // jsdomの空の算出値を、ブラウザーの初期値へ揃える
  // Supply browser initial computed values that jsdom leaves empty
  const defaults = document.createElement("style");
  defaults.textContent = "* { position: static; overflow: visible; overflow-x: visible; overflow-y: visible; transform: none; filter: none; perspective: none; backdrop-filter: none; will-change: auto; contain: none; clip-path: none; border-width: 0px; }";
  document.body.append(defaults);
  element = document.createElement("div");
  element.dataset.testid = "hud";
  element.innerText = "Height: 1";
  document.body.append(element);
  vi.spyOn(element, "getBoundingClientRect").mockReturnValue(new DOMRect(10, 10, 100, 20));
  Object.defineProperty(document, "elementFromPoint", { configurable: true, value: vi.fn(() => document.body) });
  dispose = initDomQueryResponder();
});

afterEach(() => {
  dispose();
  document.body.replaceChildren();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function query() {
  useTopicStore.setState({ topics: { [Topics.playtestDomQuery]: { requestId: "request", testid: "hud" } } });
  return vi.mocked(sendAction).mock.calls[0][1];
}

describe("DOMの可視性とクリック可否", () => {
  it("クリックを透過するHUDも本文待ちには可視として返す", () => {
    element.style.pointerEvents = "none";
    expect(query()).toMatchObject({ found: true, visible: true, hitTestPassed: false, text: "Height: 1" });
  });

  it("非表示要素は可視にもクリック可能にもならない", () => {
    element.hidden = true;
    expect(query()).toMatchObject({ found: true, visible: false, hitTestPassed: false });
  });

  it("中心に実ヒットする要素はクリック可能", () => {
    vi.mocked(document.elementFromPoint).mockReturnValue(element);
    expect(query()).toMatchObject({ found: true, visible: true, hitTestPassed: true });
  });

  it("存在しない要素は可視にしない", () => {
    element.remove();
    expect(query()).toMatchObject({ found: false, visible: false, hitTestPassed: false });
  });

  // 表示待機はクリック透過と区別し、祖先による完全な切り取りを拒む
  // Text waits reject complete ancestor clipping independently of pointer transparency
  it.each(["hidden", "auto", "scroll", "clip"])("overflow:%s祖先の外は表示済みにしない", (overflow) => {
    const parent = document.createElement("div");
    parent.style.overflowX = overflow;
    parent.style.overflowY = overflow;
    document.body.append(parent);
    parent.append(element);
    vi.spyOn(parent, "getBoundingClientRect").mockReturnValue(new DOMRect(200, 200, 300, 100));
    vi.mocked(element.getBoundingClientRect).mockReturnValue(new DOMRect(200, 340, 170, 28));
    expect(query()).toMatchObject({ found: true, visible: false, hitTestPassed: false });
  });

  it.each(["x", "y"])("overflow-%s:clipは可視の他軸を切らない", (axis) => {
    const parent = document.createElement("div");
    parent.style.position = "relative";
    parent.style.overflowX = axis === "x" ? "clip" : "visible";
    parent.style.overflowY = axis === "y" ? "clip" : "visible";
    document.body.append(parent);
    parent.append(element);
    vi.spyOn(parent, "getBoundingClientRect").mockReturnValue(new DOMRect(200, 200, 300, 100));
    vi.mocked(element.getBoundingClientRect).mockReturnValue(
      axis === "x" ? new DOMRect(200, 340, 170, 28) : new DOMRect(540, 200, 170, 28),
    );
    vi.mocked(document.elementFromPoint).mockReturnValue(element);
    expect(query()).toMatchObject({ found: true, visible: true, hitTestPassed: true });
    element.dataset.tutorialAnchor = "hud";
    expect(resolveTutorialAnchor("hud")).toMatchObject({
      status: "ready",
      clip: axis === "x"
        ? { left: 200, right: 500, top: 0, bottom: innerHeight }
        : { left: 0, right: innerWidth, top: 200, bottom: 300 },
    });
  });

  it("クリップ内に一部が残る透過HUDは表示済みとする", () => {
    const parent = document.createElement("div");
    parent.style.overflowX = "hidden";
    parent.style.overflowY = "hidden";
    document.body.append(parent);
    parent.append(element);
    element.style.pointerEvents = "none";
    vi.spyOn(parent, "getBoundingClientRect").mockReturnValue(new DOMRect(200, 200, 300, 100));
    vi.mocked(element.getBoundingClientRect).mockReturnValue(new DOMRect(200, 290, 170, 28));
    expect(query()).toMatchObject({ found: true, visible: true, hitTestPassed: false });
  });

  it("viewportを包含ブロックとするfixed要素は祖先のoverflowから脱出する", () => {
    const parent = document.createElement("div");
    parent.style.overflowX = "hidden";
    parent.style.overflowY = "hidden";
    document.body.append(parent);
    parent.append(element);
    element.style.position = "fixed";
    vi.spyOn(parent, "getBoundingClientRect").mockReturnValue(new DOMRect(200, 200, 300, 100));
    expect(query()).toMatchObject({ found: true, visible: true });
  });
});
