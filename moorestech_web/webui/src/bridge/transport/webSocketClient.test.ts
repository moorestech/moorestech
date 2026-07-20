import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const openedUrls: string[] = [];
const sockets: WebSocketStub[] = [];

class WebSocketStub {
  static readonly OPEN = 1;
  readyState = 0;
  sent: string[] = [];
  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;

  constructor(url: string | URL) {
    openedUrls.push(String(url));
    sockets.push(this);
  }

  send(message: string) { this.sent.push(message); }
  close() { this.readyState = 3; this.onclose?.(); }
  open() { this.readyState = WebSocketStub.OPEN; this.onopen?.(); }
  receive(message: object) { this.onmessage?.({ data: JSON.stringify(message) }); }
}

beforeEach(() => {
  // シングルトン分離とブラウザ境界の置換
  // Isolate the singleton and replace the browser boundary
  vi.resetModules();
  openedUrls.length = 0;
  sockets.length = 0;
  vi.stubGlobal("location", { host: "example.test" });
  vi.stubGlobal("WebSocket", WebSocketStub);
});

afterEach(() => {
  vi.useRealTimers();
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
    deliverTopicPayload(Topics.modal, 1, { modal: undefined });
    subscriptions.release(Topics.modal);

    expect(readTopic(Topics.modal)).toEqual({ modal: undefined });
  });

  it("切断後に全購読 topic の snapshot が揃うまで restoring を維持する", async () => {
    vi.useFakeTimers();
    const { initBridge } = await import("./webSocketClient");
    const { Topics } = await import("./protocol");
    const { useTopicStore } = await import("../store/topicStore");
    initBridge();
    sockets[0].open();
    expect(useTopicStore.getState().status).toBe("restoring");
    for (const topic of [Topics.modal, Topics.blockInventory, Topics.uiState, Topics.inventory]) {
      sockets[0].receive({ op: "snapshot", topic, revision: 1, data: fixtureFor(topic) });
    }
    expect(useTopicStore.getState().status).toBe("open");
    sockets[0].receive({ op: "event", topic: Topics.uiState, revision: 2, data: { state: "PlayerInventory" } });

    sockets[0].close();
    expect(useTopicStore.getState().status).toBe("reconnecting");
    await vi.advanceTimersByTimeAsync(100);
    sockets[1].open();
    expect(useTopicStore.getState().status).toBe("restoring");
    for (const topic of [Topics.modal, Topics.blockInventory, Topics.uiState, Topics.inventory]) {
      sockets[1].receive({ op: "snapshot", topic, revision: 0, data: fixtureFor(topic) });
    }
    expect(useTopicStore.getState().status).toBe("open");
    expect(useTopicStore.getState().topics[Topics.uiState]).toEqual({ state: "GameScreen" });
  });

  it("pong が途絶えると socket を閉じて再接続状態へ移る", async () => {
    vi.useFakeTimers();
    const { initBridge } = await import("./webSocketClient");
    const { useTopicStore } = await import("../store/topicStore");
    initBridge();
    sockets[0].open();

    await vi.advanceTimersByTimeAsync(15000);

    expect(useTopicStore.getState().status).toBe("reconnecting");
  });
});

function fixtureFor(topic: string) {
  if (topic === "ui.modal") return {};
  if (topic === "block_inventory.current") return { open: false };
  if (topic === "ui_state.current") return { state: "GameScreen" };
  return { mainSlots: [], hotbarSlots: [], grab: { itemId: 0, count: 0 }, selectedHotbar: 0 };
}
