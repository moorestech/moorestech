// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useTopicStore } from "../store/topicStore";
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { initDomQueryResponder } from "./domQueryResponder";

vi.mock("../transport/webSocketClient", () => ({ sendAction: vi.fn().mockResolvedValue({ ok: true }) }));
vi.mock("../transport/subscriptionManager", () => ({ subscriptions: { acquire: vi.fn(), release: vi.fn() } }));

let dispose: () => void;
let element: HTMLElement;

beforeEach(() => {
  vi.mocked(sendAction).mockClear().mockResolvedValue({ ok: true });
  vi.stubGlobal("CSS", { escape: (value: string) => value });
  useTopicStore.setState({ topics: {}, status: "open" });
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
});
