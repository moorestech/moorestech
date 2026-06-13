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

  // from の count 個を to へ移す最小モデル（空なら itemId コピー、同種なら加算）
  // Minimal model: move count items from→to (copy itemId when empty, add when same item)
  const applyMove = (p: ActionPayloads["inventory.move_item"]) => {
    const from = slotOf(p.from);
    const to = slotOf(p.to);
    if (to.count === 0) to.itemId = from.itemId;
    to.count += p.count;
    from.count -= p.count;
    if (from.count <= 0) {
      from.count = 0;
      from.itemId = 0;
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
      // result(ack) は即時、topic event は数十ms 後に別経路で push（stale grab 再現）
      // ack is immediate; the topic event is pushed later on a separate channel (reproduces stale grab)
      const ok = msg.type !== "fail.always";
      send(ws, { op: "result", requestId: msg.requestId, ok, error: ok ? undefined : "mock_error" });
      if (msg.type === "inventory.move_item") {
        applyMove(msg.payload as ActionPayloads["inventory.move_item"]);
        setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
      }
      if (msg.type === "inventory.collect") {
        inv = clone(fx.inventoryAfterCollect);
        setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
      }
      return;
    }
  });
});

server.listen(PORT, () => console.log(`mock-host on ${PORT}`));
