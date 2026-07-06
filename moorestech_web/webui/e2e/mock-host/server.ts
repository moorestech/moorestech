import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer, type WebSocket } from "ws";
import type { ClientMsg, ActionPayloads } from "../../src/bridge/protocol";
import { Topics } from "../../src/bridge/protocol";
import type {
  PlayerInventoryData,
  SlotData,
  SlotRef,
  BlockInventoryData,
  BlockSlotRef,
  ModalRequest,
  UiStateData,
} from "../../src/bridge/payloadTypes";
import * as fx from "./fixtures";

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));
const PORT = Number(process.env.MOCK_PORT ?? 5273);

// 受信 action を記録（送信契約 assert 用に /__actions で返す）
// Record received actions (exposed at /__actions to assert the send contract)
const received: { type: string; payload: unknown }[] = [];

// テスト専用の現在ブロック。既定は閉(open:false)で、/__block?type=chest|tank|closed で切替
// Test-only current block; defaults to closed (open:false), switch via /__block?type=chest|tank|closed
// 既定を閉にするのは、開いた panel が他テストの画面を覆って干渉しないため
// Default-closed so an open panel never overlays and interferes with other tests
let currentBlock: BlockInventoryData = clone(fx.blockClosed);
const blockSubscribers = new Set<WebSocket>();

// モーダルは既定で非表示。/__modal?show=1 で表示（全面 backdrop が他テストを妨げないよう opt-in）
// Modal is hidden by default; show via /__modal?show=1 (opt-in so the full-screen backdrop never blocks other tests)
let currentModal: ModalRequest | null = null;
const modalSubscribers = new Set<WebSocket>();

// ui_state は既定でインベントリ画面。/__uistate?state=X で切替（既存 e2e の表示前提を守る既定値）
// ui_state defaults to the inventory screen; switch via /__uistate?state=X (default keeps existing e2e assumptions)
let currentUiState: UiStateData = clone(fx.uiState);
const uiStateSubscribers = new Set<WebSocket>();

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
  // テスト用: 配信するブロックインベントリを差し替えて購読者へ event push
  // Test-only: swap the served block inventory and push an event to subscribers
  if (url.startsWith("/__block")) {
    const type = new URL(url, "http://x").searchParams.get("type") ?? "chest";
    currentBlock = clone(type === "tank" ? fx.blockTank : type === "closed" ? fx.blockClosed : fx.blockChest);
    for (const ws of blockSubscribers) send(ws, { op: "event", topic: Topics.blockInventory, data: currentBlock });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
    return;
  }
  // テスト用: モーダルの表示/非表示を切替えて購読者へ event push
  // Test-only: toggle the modal and push an event to subscribers
  if (url.startsWith("/__modal")) {
    const show = new URL(url, "http://x").searchParams.get("show") === "1";
    currentModal = show ? clone(fx.modalSample) : null;
    for (const ws of modalSubscribers) send(ws, { op: "event", topic: Topics.modal, data: { modal: currentModal } });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
    return;
  }
  // テスト用: ui_state を差し替えて購読者へ event push
  // Test-only: swap the served ui_state and push an event to subscribers
  if (url.startsWith("/__uistate")) {
    const state = new URL(url, "http://x").searchParams.get("state") ?? "PlayerInventory";
    currentUiState = { state };
    for (const ws of uiStateSubscribers) send(ws, { op: "event", topic: Topics.uiState, data: currentUiState });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
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

function clone<T>(o: T): T {
  return JSON.parse(JSON.stringify(o)) as T;
}

// 本番 host の NullValueHandling.Ignore と同形状にするため、送信直前に null 値キーを再帰的に除去する
// Recursively drop null-valued keys right before send to match the real host's NullValueHandling.Ignore shape
function stripNulls(value: unknown): unknown {
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

function send(ws: WebSocket, obj: unknown) {
  ws.send(JSON.stringify(stripNulls(obj)));
}

// 本番 dispatcher が受理する既知 action type。未知は unknown_action で拒否する
// Action types the real dispatcher accepts; unknown ones are rejected with unknown_action
const KNOWN_ACTIONS = new Set<string>([
  "inventory.move_item",
  "inventory.split",
  "inventory.collect",
  "inventory.sort",
  "inventory.select_hotbar",
  "craft.execute",
  "ui.modal.respond",
  "block_inventory.move_item",
  "ui_state.request",
  "debug.echo",
]);

// インベントリ状態は接続ごとに分離する。並列テストが同一 inv を奪い合わないため
// Inventory state is isolated per connection so parallel tests don't race on the same inv
wss.on("connection", (ws) => {
  let inv: PlayerInventoryData = clone(fx.inventory);

  const slotOf = (ref: SlotRef): SlotData => {
    if (ref.area === "grab") return inv.grab;
    const list = ref.area === "main" ? inv.mainSlots : inv.hotbarSlots;
    return list[ref.slot];
  };

  // block 領域はテスト用 currentBlock を、それ以外は接続ごとの inv を参照する
  // The block area refers to the test-only currentBlock; other areas refer to the per-connection inv
  const blockSlotOf = (ref: BlockSlotRef): SlotData => {
    if (ref.area !== "block") return slotOf(ref as SlotRef);
    // block 操作は開状態でのみ発生する。閉なら空スロット扱いで安全に倒す
    // Block ops only happen while open; treat a closed block as an empty slot to stay safe
    if (!currentBlock.open) return { itemId: 0, count: 0 };
    return currentBlock.itemSlots[ref.slot];
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

  // ブロック⇔プレイヤー間の移動。block 領域を跨ぐ点以外は applyMove と同型
  // Block⇔player move; same shape as applyMove except it can span the block area
  const applyBlockMove = (p: ActionPayloads["block_inventory.move_item"]): string | null => {
    const from = blockSlotOf(p.from);
    const to = blockSlotOf(p.to);
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
    if (topic === Topics.blockInventory) return currentBlock;
    if (topic === Topics.modal) return { modal: currentModal };
    if (topic === Topics.progress) return fx.progressSample;
    if (topic === Topics.uiState) return currentUiState;
    return undefined;
  };

  ws.on("close", () => {
    blockSubscribers.delete(ws);
    modalSubscribers.delete(ws);
    uiStateSubscribers.delete(ws);
  });

  ws.on("message", (raw) => {
    const msg = JSON.parse(raw.toString()) as ClientMsg;
    if (msg.op === "subscribe") {
      for (const topic of msg.topics) {
        if (topic === Topics.blockInventory) blockSubscribers.add(ws);
        if (topic === Topics.modal) modalSubscribers.add(ws);
        if (topic === Topics.uiState) uiStateSubscribers.add(ws);
        const data = topicData(topic);
        if (data !== undefined) send(ws, { op: "snapshot", topic, data });
      }
      return;
    }
    // 購読解除: グローバル購読 Set から除去する（本番 host が unsubscribe を尊重するのに合わせる）
    // Unsubscribe: remove from the global subscription Sets (mirrors the real host honoring unsubscribe)
    if (msg.op === "unsubscribe") {
      for (const topic of msg.topics) {
        if (topic === Topics.blockInventory) blockSubscribers.delete(ws);
        if (topic === Topics.modal) modalSubscribers.delete(ws);
        if (topic === Topics.uiState) uiStateSubscribers.delete(ws);
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
      } else if (msg.type === "inventory.select_hotbar") {
        // 選択 index を更新して inventory topic を再配信
        // Update the selected index and republish the inventory topic
        const index = (msg.payload as ActionPayloads["inventory.select_hotbar"]).index;
        if (typeof index === "number" && index >= 0 && index < inv.hotbarSlots.length) {
          inv.selectedHotbar = index;
          setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else {
          error = "invalid_index";
        }
      } else if (msg.type === "ui.modal.respond") {
        // どの結果でもモーダルを閉じ、全 modal 購読者へ modal:null を push
        // Any result closes the modal and pushes modal:null to all modal subscribers
        currentModal = null;
        setTimeout(() => {
          for (const sub of modalSubscribers) send(sub, { op: "event", topic: Topics.modal, data: { modal: null } });
        }, 30);
      } else if (msg.type === "block_inventory.move_item") {
        const moveError = applyBlockMove(msg.payload as ActionPayloads["block_inventory.move_item"]);
        if (moveError) {
          error = moveError;
        } else {
          setTimeout(() => {
            send(ws, { op: "event", topic: Topics.inventory, data: inv });
            send(ws, { op: "event", topic: Topics.blockInventory, data: currentBlock });
          }, 30);
        }
      } else if (msg.type === "ui_state.request") {
        // 実 host の許可制を再現: GameScreen/PlayerInventory のみ受理し、GameScreen 遷移では block も閉じる
        // Mirror the real host's allowlist: accept only GameScreen/PlayerInventory; GameScreen also closes the block
        const state = (msg.payload as ActionPayloads["ui_state.request"]).state;
        if (state !== "GameScreen" && state !== "PlayerInventory") {
          error = "unsupported_state";
        } else {
          currentUiState = { state };
          if (state === "GameScreen") currentBlock = clone(fx.blockClosed);
          setTimeout(() => {
            for (const sub of uiStateSubscribers) send(sub, { op: "event", topic: Topics.uiState, data: currentUiState });
            if (state === "GameScreen") {
              for (const sub of blockSubscribers) send(sub, { op: "event", topic: Topics.blockInventory, data: currentBlock });
            }
          }, 30);
        }
      } else if (msg.type !== "fail.always" && !KNOWN_ACTIONS.has(msg.type)) {
        // 未知 action type は本番 dispatcher と同じく unknown_action で拒否する（既知だが未実装の split/sort/craft は no-op で ok:true）
        // Unknown action types are rejected with unknown_action like the real dispatcher (known-but-unimplemented split/sort/craft stay no-op ok:true)
        error = "unknown_action";
      }
      send(ws, { op: "result", requestId: msg.requestId, ok: error === undefined, error });
      return;
    }
  });
});

server.listen(PORT, () => console.log(`mock-host on ${PORT}`));
