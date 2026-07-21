import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { Topics } from "../../src/bridge/transport/protocol";
import type { BlockInventoryData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, connections, subscribersOf } from "./state";
import { applyTopicControl, serveDictionary } from "./topics/topicControls";
import { contentType, injectDemoBackground, placeholderIcon, realIconFor } from "./assets/demoAssets";
export { injectDemoBackground } from "./assets/demoAssets";

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
export function createMockHttpServer(): Server {
  return createServer(async (req, res) => {
    const url = req.url ?? "/";
    if (url.startsWith("/api/i18n/")) {
      serveDictionary(url, res);
      return;
    }
    if (url === "/api/master/items") {
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify(DEMO ? fx.demoItemMaster : state.itemMaster));
      return;
    }
    if (url === "/__actions") {
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify(received));
      return;
    }
    if (url.startsWith("/__item-master")) {
      const name = new URL(url, "http://x").searchParams.get("woodName") ?? "Wood";
      state.itemMaster = clone(fx.itemMaster);
      state.itemMaster.items[0].name = name;
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
      state.currentUiState = { state: uiState, subState: subState as "GameScreen" | "PauseMenuScreen" | undefined };
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
    // テスト用: ワールドピンの射影値を差し替えて購読者へ event push（clear=1で全消去）
    // Test-only: replace the projected world pins and push an event to subscribers (clear=1 empties them)
    if (url.startsWith("/__worldpin")) {
      const params = new URL(url, "http://x").searchParams;
      const pins = params.get("clear") === "1" ? [] : [{
        pinId: params.get("id") ?? "map-object-pin",
        text: params.get("text") ?? "Pin",
        screenX: Number(params.get("x") ?? 0.5),
        screenY: Number(params.get("y") ?? 0.5),
        onScreen: params.get("on") !== "0",
        directionX: Number(params.get("dx") ?? 0),
        directionY: Number(params.get("dy") ?? 0),
      }];
      state.worldPins = { revision: state.worldPins.revision + 1, pins };
      for (const ws of subscribersOf(Topics.worldPins)) send(ws, { op: "event", topic: Topics.worldPins, data: state.worldPins });
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/__gamestate")) {
      const value = new URL(url, "http://x").searchParams.get("state") ?? "InGame";
      state.gameState = { state: value as "InGame" | "Skit" | "CutScene" };
      for (const ws of subscribersOf(Topics.gameState)) send(ws, { op: "event", topic: Topics.gameState, data: state.gameState });
      res.end(JSON.stringify({ ok: true }));
      return;
    }
    if (url.startsWith("/__skit")) {
      const params = new URL(url, "http://x").searchParams;
      // S1のshow契約を維持しつつ、S2/S3の段階fixtureを選択する
      // Preserve the S1 show contract while selecting S2/S3 staged fixtures
      const stage = params.get("stage") ?? (params.get("show") === "1" ? "background" : "none");
      state.skitPresentation = stage === "text" ? clone(fx.blockingSkitText)
        : stage === "choices" ? clone(fx.blockingSkitChoices)
        : stage === "background" ? {
        ...clone(fx.skitPresentation), sessionId: "bg-1", sceneRevision: 1,
        presentationState: { ...clone(fx.skitPresentation.presentationState), mode: "background",
          speakerName: "Moore", body: "Background message", textAreaVisible: true,
          textReveal: { mode: "instant", intervalMs: 0 } },
      } : clone(fx.skitPresentation);
      for (const ws of subscribersOf(Topics.skitPresentation)) send(ws, { op: "event", topic: Topics.skitPresentation, data: state.skitPresentation });
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
    res.setHeader("content-type", contentType(extname(path)));
    res.end(rel === "/index.html" ? injectDemoBackground(data.toString("utf8"), DEMO) : data);
  });
}
