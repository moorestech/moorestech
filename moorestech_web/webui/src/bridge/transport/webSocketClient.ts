// Unity 側 Web UI ホストと通信する WebSocket クライアント（純粋なトランスポート）
// WebSocket client that talks to the Unity-side Web UI host (a pure transport)
import { Topics, type ServerMsg, type ClientMsg, type ActionResult, type TopicPayloads } from "./protocol";
import { deliverTopicPayload, useTopicStore } from "../store/topicStore";
import { subscriptions } from "./subscriptionManager";

export type { ActionResult };

// 壊れたフレームで handler が落ちるのを防ぐ JSON.parse ラッパ。
// AGENTS規約「try-catch原則禁止」の正当な例外として、try-catch をここに隔離し呼び出し側は null 分岐
// Guarded JSON.parse so a broken frame can't crash the handler.
// Justified exception to the no-try-catch rule: try-catch is isolated here; callers branch on null
function safeParse(raw: string): Partial<ServerMsg> | null {
  try {
    return JSON.parse(raw) as Partial<ServerMsg>;
  } catch {
    return null;
  }
}

class WebSocketClient {
  private ws: WebSocket | null = null;
  private readonly url: string;
  private hasConnected = false;
  private reconnectDelayMs = 100;
  private nextRequestId = 1;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private lastPongAt = 0;
  private readonly pendingActions = new Map<
    string,
    { resolve: (r: ActionResult) => void; reject: (e: Error) => void; timer: number }
  >();

  constructor(url: string) {
    this.url = url;
    // 購読マネージャの送信口を自身の sendRaw に束ねる（refcount→op 送信の一方通行）
    // Bind the subscription manager's transport to this socket's sendRaw (one-way refcount→op send)
    subscriptions.setSend((msg) => this.sendRaw(msg));
    this.openSocket();
  }

  // タイムアウト・切断時は reject
  // Rejects on timeout or disconnect
  sendAction(type: string, payload: unknown): Promise<ActionResult> {
    return new Promise((resolve, reject) => {
      if (this.ws?.readyState !== WebSocket.OPEN) {
        reject(new Error("disconnected"));
        return;
      }
      const requestId = `a${this.nextRequestId++}`;
      const timer = window.setTimeout(() => {
        this.pendingActions.delete(requestId);
        reject(new Error("timeout"));
      }, 5000);
      this.pendingActions.set(requestId, { resolve, reject, timer });
      const msg: ClientMsg = { op: "action", type, requestId, payload };
      this.ws.send(JSON.stringify(msg));
    });
  }

  private sendRaw(msg: ClientMsg) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(msg));
    }
  }

  private openSocket() {
    const ws = new WebSocket(this.url);
    this.ws = ws;

    ws.onopen = () => {
      this.reconnectDelayMs = 100;
      this.hasConnected = true;
      // 接続確立を公開し、参照カウント>0 の topic を一括再購読する
      // Publish the open state and resubscribe all refcount>0 topics in one batch
      useTopicStore.getState().beginRestore(subscriptions.subscribedTopics());
      subscriptions.resubscribe();
      this.lastPongAt = Date.now();
      this.heartbeatTimer = globalThis.setInterval(() => {
        if (Date.now() - this.lastPongAt >= 15000) {
          ws.close();
          return;
        }
        this.sendRaw({ op: "ping" });
      }, 5000);
    };

    ws.onmessage = (ev) => {
      // バイナリ等の文字列以外のフレームは捨てる
      // Drop non-text frames
      if (typeof ev.data !== "string") return;
      const msg = safeParse(ev.data);
      if (!msg) return;
      if (msg.op === "pong") {
        this.lastPongAt = Date.now();
        return;
      }
      if (msg.op === "result") {
        if (typeof msg.requestId !== "string") return;
        const pending = this.pendingActions.get(msg.requestId);
        if (!pending) return;
        this.pendingActions.delete(msg.requestId);
        window.clearTimeout(pending.timer);
        pending.resolve({ ok: msg.ok === true, error: msg.error });
        return;
      }
      if (msg.op !== "snapshot" && msg.op !== "event") return;
      if (typeof msg.topic !== "string") return;
      if (typeof msg.revision !== "number" || !Number.isSafeInteger(msg.revision) || msg.revision < 0) return;
      // topic 配信の単一チェックポイント: バリデーション→ストア反映（違反は deliver 内で破棄）
      // Single choke point for topic delivery: validate → store write (violations are dropped inside deliver)
      deliverTopicPayload(msg.topic, msg.revision, msg.data);
    };

    ws.onerror = () => {
      // onerror 後は onclose が続くので特に何もしない
      // onerror is followed by onclose, no action needed here
    };

    ws.onclose = () => {
      if (this.heartbeatTimer !== null) {
        globalThis.clearInterval(this.heartbeatTimer);
        this.heartbeatTimer = null;
      }
      // 切断時は保留中の action を全て reject する
      // Reject all pending actions on disconnect
      this.pendingActions.forEach((p) => {
        window.clearTimeout(p.timer);
        p.reject(new Error("disconnected"));
      });
      this.pendingActions.clear();
      this.ws = null;
      // 一度でも接続していれば reconnecting、未接続なら connecting のまま
      // reconnecting if we ever connected, otherwise stay connecting
      useTopicStore.getState().setStatus(this.hasConnected ? "reconnecting" : "connecting");
      // 指数バックオフで再接続（上限 5 秒）
      // Exponential backoff reconnect (capped at 5s)
      const delay = Math.min(this.reconnectDelayMs, 5000);
      this.reconnectDelayMs = Math.min(this.reconnectDelayMs * 2, 5000);
      setTimeout(() => this.openSocket(), delay);
    };
  }
}

// 明示初期化後だけ接続を保持する
// Keep the connection only after explicit initialization
let client: WebSocketClient | null = null;

// 命令的読み出し対象は React のマウント状態ではなく bridge の生存期間に結び付ける
// Tie imperative-read topics to the bridge lifetime instead of React mount state
const PINNED_TOPICS = [
  Topics.modal,
  Topics.blockInventory,
  Topics.uiState,
  Topics.inventory,
] as const satisfies readonly (keyof TopicPayloads)[];

export function initBridge() {
  if (client !== null) return;
  PINNED_TOPICS.forEach((topic) => subscriptions.acquire(topic));
  client = new WebSocketClient(`ws://${location.host}/ws`);
}

// UI コードは原則 actions.ts の dispatchAction を使うこと（reject の処理が必要なため）
// UI code should normally use dispatchAction in actions.ts, which handles rejections
export function sendAction(type: string, payload: unknown): Promise<ActionResult> {
  if (client === null) return Promise.reject(new Error("disconnected"));
  return client.sendAction(type, payload);
}
