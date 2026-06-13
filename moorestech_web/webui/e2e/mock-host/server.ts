import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer, type WebSocket } from "ws";
import type { ClientMsg, ActionPayloads } from "../../src/bridge/protocol";
import { Topics } from "../../src/bridge/protocol";
import type { PlayerInventoryData, SlotData, SlotRef } from "../../src/bridge/payloadTypes";
import * as fx from "./fixtures";

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));
const PORT = Number(process.env.MOCK_PORT ?? 5273);

// 受信 action を記録（送信契約 assert 用に /__actions で返す）
// Record received actions (exposed at /__actions to assert the send contract)
const received: { type: string; payload: unknown }[] = [];

const MIME: Record<string, string> = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
};

const server = createServer(async (req, res) => {
  const url = req.url ?? "/";
  if (url === "/api/master/items") {
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify(fx.itemMaster));
    return;
  }
  if (url === "/__actions") {
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify(received));
    return;
  }
  if (url.startsWith("/api/icons/")) {
    // アイコンは 404 にして UI の #id フォールバックに任せる
    // Return 404 for icons; the UI falls back to the #id label
    res.statusCode = 404;
    res.end();
    return;
  }
  // 静的配信（SPA なので未知パスは index.html）
  // Static serving; unknown paths fall back to index.html (SPA)
  const rel = url === "/" ? "/index.html" : url.split("?")[0];
  const path = normalize(join(DIST, rel));
  const data = await readFile(path).catch(() => null);
  if (data === null) {
    const html = await readFile(join(DIST, "index.html"));
    res.setHeader("content-type", "text/html");
    res.end(html);
    return;
  }
  res.setHeader("content-type", MIME[extname(path)] ?? "application/octet-stream");
  res.end(data);
});

const wss = new WebSocketServer({ server, path: "/ws" });

const clone = <T>(o: T): T => JSON.parse(JSON.stringify(o)) as T;

function send(ws: WebSocket, obj: unknown) {
  ws.send(JSON.stringify(obj));
}

// インベントリ状態は接続ごとに分離する。並列テストが同一 inv を奪い合わないため
// Inventory state is isolated per connection so parallel tests don't race on the same inv
wss.on("connection", (ws) => {
  let inv: PlayerInventoryData = clone(fx.inventory);

  const slotOf = (ref: SlotRef): SlotData => {
    if (ref.area === "grab") return inv.grab;
    const list = ref.area === "main" ? inv.mainSlots : inv.hotbarSlots;
    return list[ref.slot];
  };

  // from の count 個を to へ移す最小モデル。空・数量不足は host と同じエラーコードを返す（成功は null）
  // Minimal model: move count items from→to; empty/insufficient return the host's error codes (null on success)
  const applyMove = (p: ActionPayloads["inventory.move_item"]): string | null => {
    const from = slotOf(p.from);
    const to = slotOf(p.to);
    if (from.count === 0) return "empty_slot";
    if (from.count < p.count) return "insufficient_count";
    if (to.count === 0) to.itemId = from.itemId;
    to.count += p.count;
    from.count -= p.count;
    if (from.count <= 0) {
      from.count = 0;
      from.itemId = 0;
    }
    return null;
  };

  // host と同じく mock 自身の現在 grab 状態で集積先を決め、同種スタックを集約する
  // Like the host, decide the target from the mock's own current grab and consolidate same-type stacks
  const applyCollect = (p: ActionPayloads["inventory.collect"]) => {
    const grabHeld = inv.grab.count > 0;
    const target = grabHeld ? inv.grab : slotOf(p.slot);
    if (target.count === 0) return;
    for (const s of [...inv.mainSlots, ...inv.hotbarSlots]) {
      if (s === target || s.itemId !== target.itemId || s.count === 0) continue;
      target.count += s.count;
      s.count = 0;
      s.itemId = 0;
    }
  };

  const topicData = (topic: string): unknown => {
    if (topic === Topics.inventory) return inv;
    if (topic === Topics.craftRecipes) return fx.craftRecipes;
    if (topic === Topics.machineRecipes) return fx.machineRecipes;
    if (topic === Topics.itemList) return fx.itemList;
    return undefined;
  };

  ws.on("message", (raw) => {
    const msg = JSON.parse(raw.toString()) as ClientMsg;
    if (msg.op === "subscribe") {
      for (const topic of msg.topics) {
        const data = topicData(topic);
        if (data !== undefined) send(ws, { op: "snapshot", topic, data });
      }
      return;
    }
    if (msg.op === "action") {
      received.push({ type: msg.type, payload: msg.payload });
      // ack は実 host 同様 apply 後に確定し、topic event は数十ms 後に別経路で push（stale grab 再現）
      // ack is decided after apply like the real host; the topic event is pushed later on a separate channel
      let error: string | undefined;
      if (msg.type === "fail.always") {
        error = "mock_error";
      } else if (msg.type === "inventory.move_item") {
        // 状態が変化したときだけ topic event を流す（host の失敗は packet を出さない）
        // Emit a topic event only when state changed (the host's failed move sends no packet)
        const moveError = applyMove(msg.payload as ActionPayloads["inventory.move_item"]);
        if (moveError) error = moveError;
        else setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
      } else if (msg.type === "inventory.collect") {
        applyCollect(msg.payload as ActionPayloads["inventory.collect"]);
        setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
      }
      send(ws, { op: "result", requestId: msg.requestId, ok: error === undefined, error });
      return;
    }
  });
});

server.listen(PORT, () => console.log(`mock-host on ${PORT}`));
