// Unity 側 Web UI ホストと通信する WebSocket クライアント。
// 購読モデル: subscribe / unsubscribe / snapshot の 3 種類を送り、
// snapshot / event の 2 種類を受ける。
// WebSocket client that talks to the Unity-side Web UI host.
// Subscribe-model protocol: sends subscribe / unsubscribe / snapshot,
// receives snapshot / event.

export type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown };

type Listener = (data: unknown) => void;

class WebSocketClient {
  private ws: WebSocket | null = null;
  private readonly url: string;
  private readonly listeners = new Map<string, Set<Listener>>();
  private readonly pendingSubscribes = new Set<string>();
  private reconnectDelayMs = 100;
  private closedByUs = false;

  constructor(url: string) {
    this.url = url;
  }

  connect() {
    this.closedByUs = false;
    this.openSocket();
  }

  close() {
    this.closedByUs = true;
    this.ws?.close();
    this.ws = null;
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

  private sendRaw(obj: unknown) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(obj));
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
      const msg = JSON.parse(String(ev.data)) as ServerMsg;
      if (msg.op === "snapshot" || msg.op === "event") {
        const set = this.listeners.get(msg.topic);
        if (set) set.forEach((l) => l(msg.data));
      }
    };

    ws.onerror = () => {
      // onerror 後は onclose が続くので特に何もしない
      // onerror is followed by onclose, no action needed here
    };

    ws.onclose = () => {
      this.ws = null;
      if (this.closedByUs) return;
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
client.connect();

export function subscribeTopic<T>(topic: string, listener: (data: T) => void) {
  return client.subscribe(topic, (d) => listener(d as T));
}
