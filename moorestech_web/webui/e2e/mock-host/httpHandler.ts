import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { Topics } from "../../src/bridge/transport/protocol";
import type { BlockInventoryData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, blockSubscribers, modalSubscribers, uiStateSubscribers, researchTreeSubscribers, gameStateSubscribers, skitSubscribers, connections } from "./state";

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
  gearMiner: fx.blockGearMiner,
  generic: fx.blockGeneric,
  electricToGear: fx.blockElectricToGear,
};

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));

// DEMO(採点用): 高密度データとプレースホルダアイコンを配信
// DEMO (scoring): serve dense data and placeholder icons
const DEMO = process.env.MOCK_DEMO === "1";

// bodyは高さ0なので背景を全画面固定
// The zero-height body needs a fixed full-screen background
const DEMO_BACKGROUND =
  "<div id=\"__worldbg\" style=\"position:fixed;inset:0;z-index:-1;pointer-events:none;background:url('/mock-orange-gradient.png') center/cover no-repeat\"></div>";

// デモだけ共用背景を差し込み透明HTMLを保つ
// Insert the shared image behind demo UI only, preserving transparent production-equivalent HTML
export function injectDemoBackground(html: string, demo: boolean): string {
  if (!demo) return html;
  return html.replace(/<body(\s[^>]*)?>/i, (body) => `${body}${DEMO_BACKGROUND}`);
}

// itemIdから色相を導き丸角の色付きSVGアイコンを生成（実アイコン不在時のフォールバック）
// Derive a hue from itemId and build a rounded colored SVG icon (fallback when real icons are absent)
function placeholderIcon(itemId: number): string {
  const hue = (itemId * 47) % 360;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><rect x="10" y="10" width="44" height="44" rx="2" fill="hsl(${hue} 40% 52%)" stroke="hsl(${hue} 35% 34%)" stroke-width="2"/><path d="M12 52V12H52" fill="none" stroke="hsl(${hue} 45% 68%)" stroke-width="1"/><path d="M12 52H52V12" fill="none" stroke="hsl(${hue} 35% 38%)" stroke-width="1"/><rect x="18" y="20" width="28" height="9" fill="hsl(${hue} 42% 62%)"/><rect x="18" y="34" width="28" height="9" fill="hsl(${hue} 38% 44%)"/></svg>`;
}

// 実ゲームアイコン(../moorestech_master)をitemIdへ決定的に割当てる。白背景写真なので正本と同じ画作りになる
// Map real game icons (../moorestech_master) to itemIds deterministically; white-bg photos match the reference look
import { readdirSync, readFileSync, existsSync } from "node:fs";
import { resolve as resolvePath, join as joinPath } from "node:path";

const REAL_ICON_DIR = process.env.MOCK_ICON_DIR
  ?? resolvePath(process.cwd(), "../../../moorestech_master/server_v8/mods/moorestechAlphaMod_8/assets/item");
const realIconFiles: string[] = existsSync(REAL_ICON_DIR)
  ? readdirSync(REAL_ICON_DIR).filter((f) => f.endsWith(".jpeg") || f.endsWith(".jpg")).sort()
  : [];

function realIconFor(itemId: number): Buffer | null {
  if (realIconFiles.length === 0) return null;
  return readFileSync(joinPath(REAL_ICON_DIR, realIconFiles[itemId % realIconFiles.length]));
}

const MIME: Record<string, string> = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".png": "image/png",
};

export function createMockHttpServer(): Server {
  return createServer(async (req, res) => {
    const url = req.url ?? "/";
    if (url.startsWith("/api/i18n/")) {
      // i18n辞書はe2eでは空辞書を返しkey表示にフォールバックさせる
      // Serve an empty dictionary in e2e so views fall back to key display
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({}));
      return;
    }
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
    // 次の指定actionだけを失敗させ、良性/実エラーのtoast経路を決定的に駆動する
    // Fail only the next matching action to deterministically drive benign/real toast paths
    if (url.startsWith("/__action-error")) {
      const params = new URL(url, "http://x").searchParams;
      const type = params.get("type");
      const error = params.get("error");
      state.injectedActionError = type && error ? { type, error } : null;
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // 初回snapshot遅延と接続切断をHTTPから制御する
    // Control initial snapshot delay and connection drops over HTTP
    if (url.startsWith("/__snapshot-delay")) {
      const params = new URL(url, "http://x").searchParams;
      state.snapshotDelayMs = Number(params.get("ms") ?? 0);
      state.snapshotDelayTopic = params.get("topic");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/__disconnect")) {
      const holdMs = Number(new URL(url, "http://x").searchParams.get("holdMs") ?? 0);
      state.rejectConnectionsUntil = Date.now() + holdMs;
      for (const ws of connections) ws.close();
      res.end(JSON.stringify({ ok: true }));
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
    if (url.startsWith("/__gamestate")) {
      const value = new URL(url, "http://x").searchParams.get("state") ?? "InGame";
      state.gameState = { state: value as "InGame" | "Skit" | "CutScene" };
      for (const ws of gameStateSubscribers) send(ws, { op: "event", topic: Topics.gameState, data: state.gameState });
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/__skit")) {
      const show = new URL(url, "http://x").searchParams.get("show") === "1";
      state.skitPresentation = show ? {
        ...clone(fx.skitPresentation), sessionId: "bg-1", sceneRevision: 1,
        presentationState: { ...clone(fx.skitPresentation.presentationState), mode: "background",
          speakerName: "Moore", body: "Background message", textAreaVisible: true,
          textReveal: { mode: "instant", intervalMs: 0 } },
      } : clone(fx.skitPresentation);
      for (const ws of skitSubscribers) send(ws, { op: "event", topic: Topics.skitPresentation, data: state.skitPresentation });
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/api/icons/")) {
      // DEMO時は実ゲームアイコン(無ければプレースホルダ)、通常時は404で#idフォールバック
      // DEMO serves real game icons (placeholder if absent); otherwise 404 for the #id fallback
      if (DEMO) {
        const id = Number(url.split("/api/icons/")[1]?.replace(".png", "")) || 0;
        const real = realIconFor(id);
        if (real) {
          res.setHeader("content-type", "image/jpeg");
          res.end(real);
          return;
        }
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
      const html = await readFile(join(DIST, "index.html"), "utf8");
      res.setHeader("content-type", "text/html");
      res.end(injectDemoBackground(html, DEMO));
      return;
    }
    res.setHeader("content-type", MIME[extname(path)] ?? "application/octet-stream");
    res.end(rel === "/index.html" ? injectDemoBackground(data.toString("utf8"), DEMO) : data);
  });
}
