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

    await expect(sendAction("debug.echo", {}, 5000)).rejects.toThrow("disconnected");
  });

  it("initBridge は WebSocket 接続を一度だけ開始する", async () => {
    const { initBridge } = await import("./webSocketClient");

    initBridge();
    initBridge();

    expect(openedUrls).toEqual(["ws://example.test/ws"]);
  });

  // https配信でws:のままだとmixed contentでブラウザが接続を拒否し、UIが何も描画されないまま止まる
  // Keeping ws: under https makes the browser refuse the connection outright, leaving the UI blank
  it("initBridge はページのスキームに合わせ https では wss を使う", async () => {
    const { initBridge } = await import("./webSocketClient");

    const original = location.protocol;
    Object.defineProperty(location, "protocol", { value: "https:", configurable: true });
    try {
      initBridge();
      expect(openedUrls).toEqual(["wss://example.test/ws"]);
    } finally {
      Object.defineProperty(location, "protocol", { value: original, configurable: true });
    }
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
      Topics.pauseMenu,
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
    for (const topic of [Topics.modal, Topics.blockInventory, Topics.uiState, Topics.inventory, Topics.pauseMenu]) {
      sockets[0].receive({ op: "snapshot", topic, revision: 1, data: fixtureFor(topic) });
    }
    expect(useTopicStore.getState().status).toBe("open");
    sockets[0].receive({ op: "event", topic: Topics.uiState, revision: 2, data: { state: "PlayerInventory", keyHints: [] } });

    sockets[0].close();
    expect(useTopicStore.getState().status).toBe("reconnecting");
    await vi.advanceTimersByTimeAsync(100);
    sockets[1].open();
    expect(useTopicStore.getState().status).toBe("restoring");
    for (const topic of [Topics.modal, Topics.blockInventory, Topics.uiState, Topics.inventory, Topics.pauseMenu]) {
      sockets[1].receive({ op: "snapshot", topic, revision: 0, data: fixtureFor(topic) });
    }
    expect(useTopicStore.getState().status).toBe("open");
    expect(useTopicStore.getState().topics[Topics.uiState]).toEqual({ state: "GameScreen", keyHints: [] });
  });

  // 15秒の心拍が120秒待ちのactionを先回りすると、書けたバグ報告が無言で失敗になる
  // A 15s heartbeat racing a 120s action turns a written bug report into a silent failure
  it("応答待ちの action がある間は無応答でも socket を閉じない", async () => {
    vi.useFakeTimers();
    const { initBridge, sendAction } = await import("./webSocketClient");
    const { useTopicStore } = await import("../store/topicStore");
    // action の待ちタイマーは window 経由で張られるため、偽タイマーごと window として見せる
    // The action's wait timer is set through window, so window is exposed as the faked global itself
    vi.stubGlobal("window", globalThis);
    initBridge();
    sockets[0].open();

    const pending = sendAction("bug_report.submit", {}, 120000);
    await vi.advanceTimersByTimeAsync(30000);
    expect(useTopicStore.getState().status).not.toBe("reconnecting");

    const requestId = JSON.parse(sockets[0].sent.find((raw) => raw.includes("bug_report.submit"))!).requestId;
    sockets[0].receive({ op: "result", requestId, ok: true, payload: { missing: ["video"] } });
    await expect(pending).resolves.toEqual({ ok: true, error: undefined, payload: { missing: ["video"] } });
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
  if (topic === "pause_menu.current") return { disconnected: false, bugReport: { kind: "noSession", missing: [] }, page: "top" };
  if (topic === "block_inventory.current") return { open: false };
  if (topic === "ui_state.current") return { state: "GameScreen", keyHints: [] };
  return { mainSlots: [], grab: { itemId: 0, count: 0 }, equipment: [], selectedEquipment: 0, equipmentSelectionConfirmationRevision: 0 };
}
