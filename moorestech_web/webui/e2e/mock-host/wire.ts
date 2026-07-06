import type { WebSocket } from "ws";

export function clone<T>(o: T): T {
  return JSON.parse(JSON.stringify(o)) as T;
}

// 本番 host の NullValueHandling.Ignore と同形状にするため、送信直前に null 値キーを再帰的に除去する
// Recursively drop null-valued keys right before send to match the real host's NullValueHandling.Ignore shape
export function stripNulls(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(stripNulls);
  if (value !== null && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      if (v === null) continue;
      out[k] = stripNulls(v);
    }
    return out;
  }
  return value;
}

export function send(ws: WebSocket, obj: unknown) {
  ws.send(JSON.stringify(stripNulls(obj)));
}
