import { createServer, type Server, type ServerResponse } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { Topics } from "../../src/bridge/transport/protocol";
import { BLOCK_ICON_PREFIX, FLUID_ICON_PREFIX, ITEM_ICON_PREFIX } from "../../src/bridge/transport/httpEndpoints";
import type { BlockInventoryWireData, UiStateData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, connections, subscribersOf } from "./state";
import { applyTopicControl, serveDictionary } from "./topics/topicControls";
import { applyPresentationControl } from "./topics/presentationControls";
import { contentType, fluidIconDirectory, fluidIconFiles, fluidIconSeed, iconDirectory, iconFiles, injectDemoBackground, placeholderIcon, realIconFor } from "./assets/demoAssets";
import { serveLanguageCatalog } from "./localization/transport";
export { injectDemoBackground } from "./assets/demoAssets";

// /__block?type=X で差し替える種別マップ。既定は chest（open な panel を確実に出す）
// Type map switched via /__block?type=X; defaults to chest (reliably shows an open panel)
const BLOCK_FIXTURES: Record<string, BlockInventoryWireData> = {
  chest: fx.blockChest,
  tank: fx.blockTank,
  closed: fx.blockClosed,
  machine: fx.blockMachine,
  gearMachine: fx.blockGearMachine,
  generator: fx.blockGenerator,
  miner: fx.blockMiner,
  filterSplitter: fx.blockFilterSplitter,
  gearMiner: fx.blockGearMiner,
  pump: fx.blockPump,
  pumpNoVein: fx.blockPumpNoVein,
  gearPump: fx.blockGearPump,
  generic: fx.blockGeneric,
  electricToGear: fx.blockElectricToGear,
  train: fx.trainCargo,
  trainError: fx.trainContainerMissing,
  trainPlatform: fx.blockTrainPlatform,
  trainFluidPlatform: fx.blockTrainFluidPlatform,
  electricPole: fx.blockElectricPole,
};
const DIST = fileURLToPath(new URL("../../dist", import.meta.url));
// DEMO(採点用): 高密度データとプレースホルダアイコンを配信
// DEMO (scoring): serve dense data and placeholder icons
const DEMO = process.env.MOCK_DEMO === "1";

// item/block/fluidの3分岐が共有する「実アセット→placeholder→404」の3段だけを括る(D9)
// Consolidates only the shared real-asset -> placeholder -> 404 fallback chain across the item/block/fluid branches (D9)
function respondGameIcon(res: ServerResponse, real: Buffer | null, placeholderSeed: number): void {
  if (!DEMO) {
    res.statusCode = 404;
    res.end();
    return;
  }
  if (real) {
    res.setHeader("content-type", "image/jpeg");
    res.end(real);
    return;
  }
  res.setHeader("content-type", "image/svg+xml");
  res.end(placeholderIcon(placeholderSeed));
}

export function createMockHttpServer(): Server {
  return createServer(async (req, res) => {
    const url = req.url ?? "/";
    if (serveLanguageCatalog(url, res)) return;
    if (url.startsWith("/api/i18n/")) {
      serveDictionary(url, res);
      return;
    }
    if (url === "/api/master/items") {
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify(DEMO ? fx.demoItemMaster : state.itemMaster));
      return;
    }
    if (url === "/api/master/fluids") {
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify(fx.fluidMaster));
      return;
    }
    if (url === "/__actions") {
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify(received));
      return;
    }
    if (url.startsWith("/__item-master")) {
      const itemGuid = new URL(url, "http://x").searchParams.get("woodGuid") ?? fx.WOOD_ITEM_GUID;
      state.itemMaster = clone(fx.itemMaster);
      state.itemMaster.items[0].itemGuid = itemGuid;
      res.end(JSON.stringify({ ok: true }));
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
      state.preserveInventoryOnDisconnect = true;
      for (const ws of connections) ws.close();
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/__topic-control")) {
      applyTopicControl(url, res);
      return;
    }
    // テスト用: 配信するブロックインベントリを差し替えて購読者へ event push
    // Test-only: swap the served block inventory and push an event to subscribers
    if (url.startsWith("/__block")) {
      const type = new URL(url, "http://x").searchParams.get("type") ?? "chest";
      state.currentBlock = clone(BLOCK_FIXTURES[type] ?? fx.blockChest);
      for (const ws of subscribersOf(Topics.blockInventory)) send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: モーダルの表示/非表示を切替えて購読者へ event push
    // Test-only: toggle the modal and push an event to subscribers
    if (url.startsWith("/__modal")) {
      const show = new URL(url, "http://x").searchParams.get("show") === "1";
      state.currentModal = show ? clone(fx.modalSample) : null;
      for (const ws of subscribersOf(Topics.modal)) send(ws, { op: "event", topic: Topics.modal, data: { modal: state.currentModal } });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: ui_state を差し替えて購読者へ event push
    // Test-only: swap the served ui_state and push an event to subscribers
    if (url.startsWith("/__uistate")) {
      const params = new URL(url, "http://x").searchParams;
      const uiState = params.get("state") ?? "PlayerInventory";
      const subState = params.get("subState") ?? undefined;
      state.currentUiState = { state: uiState, subState: subState as UiStateData["subState"], keyHints: fx.uiStateKeyHints };
      for (const ws of subscribersOf(Topics.uiState)) send(ws, { op: "event", topic: Topics.uiState, data: state.currentUiState });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: 乗車状態と分岐候補を差し替えて全購読者へ配信する
    // Test-only: replace riding/branch state and publish it to every subscriber.
    if (url.startsWith("/__train-riding")) {
      const params = new URL(url, "http://x").searchParams;
      state.trainRiding = {
        riding: params.get("riding") === "1",
        branchCandidateCount: Number(params.get("count") ?? 0),
        selectedBranchIndex: Number(params.get("selected") ?? 0),
      };
      for (const ws of subscribersOf(Topics.trainRiding)) send(ws, { op: "event", topic: Topics.trainRiding, data: state.trainRiding });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // テスト用: 研究ツリーをフィクスチャへ戻して購読者へ event push（テスト間の状態漏れ防止）
    // Test-only: reset the research tree to the fixture and push an event (prevents cross-test state leakage)
    if (url.startsWith("/__research")) {
      state.researchTree = clone(fx.researchTree);
      for (const ws of subscribersOf(Topics.researchTree)) send(ws, { op: "event", topic: Topics.researchTree, data: state.researchTree });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    // presentationControls.tsへ分離
    // Split into presentationControls.ts
    if (applyPresentationControl(url, res)) return;
    // アイテムとブロックのアイコンは本番 httpEndpoints と同じ2経路を同形で配信する
    // Item and block icons are served identically on the same two paths as production httpEndpoints
    const isBlockIcon = url.startsWith(BLOCK_ICON_PREFIX);
    if (url.startsWith(ITEM_ICON_PREFIX) || isBlockIcon) {
      // DEMO時は実ゲームアイコン(無ければプレースホルダ)、通常時は404で#idフォールバック
      // DEMO serves real game icons (placeholder if absent); otherwise 404 for the #id fallback
      const id = Number(url.split("/").pop()?.replace(".png", "")) || 0;
      // ブロック画像は実アセットが無いため常にプレースホルダ（実ホストはBlockImageContainerのPNG）
      // Block images have no real asset so always use the placeholder (the real host renders BlockImageContainer PNGs)
      const real = DEMO && !isBlockIcon ? realIconFor(id, iconDirectory, iconFiles) : null;
      respondGameIcon(res, real, id);
      return;
    }
    // 液体アイコンはguidベースで別配信
    // Fluid icons are served separately, keyed by guid
    if (url.startsWith(FLUID_ICON_PREFIX)) {
      // DEMOは実/プレースホルダ、他は404。guid欠落は即404
      // DEMO: real/placeholder icon; otherwise 404. A missing guid is an immediate 404
      const guid = url.split("/").pop()?.replace(".png", "") ?? "";
      if (!guid) {
        res.statusCode = 404;
        res.end();
        return;
      }
      const seed = fluidIconSeed(guid);
      const real = DEMO ? realIconFor(seed, fluidIconDirectory, fluidIconFiles) : null;
      respondGameIcon(res, real, seed);
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
    res.setHeader("content-type", contentType(extname(path)));
    res.end(rel === "/index.html" ? injectDemoBackground(data.toString("utf8"), DEMO) : data);
  });
}
