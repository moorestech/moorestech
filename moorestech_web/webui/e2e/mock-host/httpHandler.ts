import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { Topics } from "../../src/bridge/transport/protocol";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, blockSubscribers, modalSubscribers, uiStateSubscribers } from "./state";

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));

const MIME: Record<string, string> = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
};

export function createMockHttpServer(): Server {
  return createServer(async (req, res) => {
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
      state.currentBlock = clone(type === "tank" ? fx.blockTank : type === "closed" ? fx.blockClosed : fx.blockChest);
      for (const ws of blockSubscribers) send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: モーダルの表示/非表示を切替えて購読者へ event push
    // Test-only: toggle the modal and push an event to subscribers
    if (url.startsWith("/__modal")) {
      const show = new URL(url, "http://x").searchParams.get("show") === "1";
      state.currentModal = show ? clone(fx.modalSample) : null;
      for (const ws of modalSubscribers) send(ws, { op: "event", topic: Topics.modal, data: { modal: state.currentModal } });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: ui_state を差し替えて購読者へ event push
    // Test-only: swap the served ui_state and push an event to subscribers
    if (url.startsWith("/__uistate")) {
      const uiState = new URL(url, "http://x").searchParams.get("state") ?? "PlayerInventory";
      state.currentUiState = { state: uiState };
      for (const ws of uiStateSubscribers) send(ws, { op: "event", topic: Topics.uiState, data: state.currentUiState });
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
}
