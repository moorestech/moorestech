import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { Topics } from "../../src/bridge/transport/protocol";
import type { BlockInventoryData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, blockSubscribers, modalSubscribers, uiStateSubscribers, researchTreeSubscribers } from "./state";

// /__block?type=X で差し替える種別マップ。既定は chest（open な panel を確実に出す）
// Type map switched via /__block?type=X; defaults to chest (reliably shows an open panel)
const BLOCK_FIXTURES: Record<string, BlockInventoryData> = {
  chest: fx.blockChest,
  tank: fx.blockTank,
  closed: fx.blockClosed,
  machine: fx.blockMachine,
  gearMachine: fx.blockGearMachine,
  generator: fx.blockGenerator,
  miner: fx.blockMiner,
  filterSplitter: fx.blockFilterSplitter,
};

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));

// DEMO(採点用): 密度の高いデータとプレースホルダアイコンを配信するモード
// DEMO (scoring only): serve dense data and placeholder icons
const DEMO = process.env.MOCK_DEMO === "1";

// itemId から安定した色相を導き、丸角の色付きアイコンSVGを生成する
// Derive a stable hue from itemId and generate a rounded colored icon SVG
function placeholderIcon(itemId: number): string {
  const hue = (itemId * 47) % 360;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><rect x="10" y="10" width="44" height="44" rx="2" fill="hsl(${hue} 40% 52%)" stroke="hsl(${hue} 35% 34%)" stroke-width="2"/><rect x="18" y="20" width="28" height="9" fill="hsl(${hue} 42% 62%)"/><rect x="18" y="34" width="28" height="9" fill="hsl(${hue} 38% 44%)"/></svg>`;
}

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
      res.end(JSON.stringify(DEMO ? fx.demoItemMaster : fx.itemMaster));
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
      state.currentBlock = clone(BLOCK_FIXTURES[type] ?? fx.blockChest);
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
    // テスト用: 研究ツリーをフィクスチャへ戻して購読者へ event push（テスト間の状態漏れ防止）
    // Test-only: reset the research tree to the fixture and push an event (prevents cross-test state leakage)
    if (url.startsWith("/__research")) {
      state.researchTree = clone(fx.researchTree);
      for (const ws of researchTreeSubscribers) send(ws, { op: "event", topic: Topics.researchTree, data: state.researchTree });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/api/icons/")) {
      // DEMO 時は色付きプレースホルダを返し、通常時は 404 で #id フォールバックに任せる
      // In DEMO serve a colored placeholder; otherwise 404 and let the UI fall back to #id
      if (DEMO) {
        const id = Number(url.split("/api/icons/")[1]?.replace(".png", "")) || 0;
        res.setHeader("content-type", "image/svg+xml");
        res.end(placeholderIcon(id));
        return;
      }
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
