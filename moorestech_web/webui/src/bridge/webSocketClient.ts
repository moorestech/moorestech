// Unity 側 Web UI ホストと通信する WebSocket クライアント（subscribe/unsubscribe/snapshot 送信、snapshot/event 受信）。
// WebSocket client for the Unity-side Web UI host; sends subscribe/unsubscribe/snapshot, receives snapshot/event.

type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown };

type ClientMsg =
  | { op: "subscribe"; topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "snapshot"; topics: string[] };

type Listener = (data: unknown) => void;

class WebSocketClient {
  private ws: WebSocket | null = null;
  private readonly url: string;
  private readonly listeners = new Map<string, Set<Listener>>();
  private readonly pendingSubscribes = new Set<string>();
  private reconnectDelayMs = 100;

  constructor(url: string) {
    this.url = url;
    this.openSocket();
  }

  subscribe(topic: string, listener: Listener): () => void {
    let set = this.listeners.get(topic);
    if (!set) {
      set = new Set();
      this.listeners.set(topic, set);
    }
    set.add(listener);

    this.sendSubscribe(topic);

    return () => {
      set!.delete(listener);
      if (set!.size === 0) {
        this.listeners.delete(topic);
        this.sendRaw({ op: "unsubscribe", topics: [topic] });
      }
    };
  }

  private sendSubscribe(topic: string) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.sendRaw({ op: "subscribe", topics: [topic] });
    } else {
      this.pendingSubscribes.add(topic);
    }
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
      // 保留中の subscribe と、再接続時は listener が登録済みのトピックを全て再購読
      // On (re)connect, flush pending subscribes and re-subscribe known topics
      const toSub = new Set<string>();
      this.pendingSubscribes.forEach((t) => toSub.add(t));
      this.listeners.forEach((_, t) => toSub.add(t));
      this.pendingSubscribes.clear();
      if (toSub.size > 0) {
        this.sendRaw({ op: "subscribe", topics: Array.from(toSub) });
      }
    };

    ws.onmessage = (ev) => {
      // バイナリ等の文字列以外のフレームは捨てる
      // Drop non-text frames
      if (typeof ev.data !== "string") return;
      const msg = JSON.parse(ev.data) as Partial<ServerMsg>;
      if (msg.op !== "snapshot" && msg.op !== "event") return;
      if (typeof msg.topic !== "string") return;
      const set = this.listeners.get(msg.topic);
      if (set) set.forEach((l) => l(msg.data));
    };

    ws.onerror = () => {
      // onerror 後は onclose が続くので特に何もしない
      // onerror is followed by onclose, no action needed here
    };

    ws.onclose = () => {
      this.ws = null;
      // 指数バックオフで再接続（上限 5 秒）
      // Exponential backoff reconnect (capped at 5s)
      const delay = Math.min(this.reconnectDelayMs, 5000);
      this.reconnectDelayMs = Math.min(this.reconnectDelayMs * 2, 5000);
      setTimeout(() => this.openSocket(), delay);
    };
  }
}

// モジュール内シングルトンで接続を保持
// Keep the connection as a module-level singleton
const client = new WebSocketClient(`ws://${location.host}/ws`);

export function subscribeTopic<T>(topic: string, listener: (data: T) => void) {
  return client.subscribe(topic, (d) => listener(d as T));
}
