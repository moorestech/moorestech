import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const openedUrls: string[] = [];

class WebSocketStub {
  static readonly OPEN = 1;
  readonly readyState = 0;

  constructor(url: string | URL) {
    openedUrls.push(String(url));
  }

  send() {}
}

beforeEach(() => {
  // シングルトン分離とブラウザ境界の置換
  // Isolate the singleton and replace the browser boundary
  vi.resetModules();
  openedUrls.length = 0;
  vi.stubGlobal("location", { host: "example.test" });
  vi.stubGlobal("WebSocket", WebSocketStub);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("WebSocket bridge initialization", () => {
  it("import だけでは WebSocket 接続を開始しない", async () => {
    await import("./webSocketClient");

    expect(openedUrls).toEqual([]);
  });

  it("未初期化の action は disconnected として reject する", async () => {
    const { sendAction } = await import("./webSocketClient");

    await expect(sendAction("debug.echo", {})).rejects.toThrow("disconnected");
  });

  it("initBridge は WebSocket 接続を一度だけ開始する", async () => {
    const { initBridge } = await import("./webSocketClient");

    initBridge();
    initBridge();

    expect(openedUrls).toEqual(["ws://example.test/ws"]);
  });

  it("initBridge は命令的読み出し対象を pin し一時購読解除後も最新値を保持する", async () => {
    const { initBridge } = await import("./webSocketClient");
    const { subscriptions } = await import("./subscriptionManager");
    const { Topics } = await import("./protocol");
    const { deliverTopicPayload } = await import("../store/topicStore");
    const { readTopic } = await import("../store/useTopic");

    initBridge();
    expect(new Set(subscriptions.subscribedTopics())).toEqual(new Set([
      Topics.modal,
      Topics.blockInventory,
      Topics.uiState,
      Topics.inventory,
    ]));

    subscriptions.acquire(Topics.modal);
    deliverTopicPayload(Topics.modal, { modal: undefined });
    subscriptions.release(Topics.modal);

    expect(readTopic(Topics.modal)).toEqual({ modal: undefined });
  });
});
