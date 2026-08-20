// Task 9 研究UI目視QA撮影
// Task 9 visual QA capture for the research UI refresh

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";
import { WebSocketServer } from "ws";
import { researchableNodeGuid, itemLackingNodeGuid } from "./mock-host/researchFixtures";
import { settleBoundingBox } from "./support/panSettle";

const PORT = Number(process.env.CAPTURE_PORT ?? 5412);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/research-qa";
const VIEWPORT = { width: 1280, height: 720 } as const;
const WIDE_VIEWPORT = { width: 2432, height: 786 } as const;

async function openResearchTree(page: Page) {
  // mock制御はcontextにbaseURLが無いため絶対URLで叩く（capture-mining-progress.ts踏襲）
  // No context baseURL is set for capture scripts, so hit mock control with absolute URLs (follows capture-mining-progress.ts)
  await page.request.get(`http://127.0.0.1:${PORT}/__research`);
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=ResearchTree`);
  // 所持itemId1×15へ購読前に差替え
  // Swap in the itemId1×15 inventory before subscribing
  await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=researchOwnedItems`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("research-tree").waitFor();
  await page.evaluate("document.fonts.ready.then(() => undefined)");
}

// ホバー由来のツールチップを消すためカーソルを画面外へ退避してから撮る
// Move the cursor off-screen to dismiss hover tooltips before shooting
async function shoot(page: Page, name: string) {
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(OUT_DIR, name) });
}

async function captureDetailPanes(browser: Awaited<ReturnType<typeof chromium.launch>>) {
  // (b)(c) 2ノード詳細ペイン撮影
  // (b)(c) Capture the detail pane for two nodes
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });
  await openResearchTree(page);

  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  await page.getByTestId("research-detail-pane").waitFor();
  await shoot(page, "detail-researchable.png");

  await page.getByTestId(`research-node-${itemLackingNodeGuid}`).click();
  await page.getByTestId("research-detail-pane").waitFor();
  await shoot(page, "detail-item-lacking.png");

  await page.close();
}

async function captureOverview(browser: Awaited<ReturnType<typeof chromium.launch>>) {
  // (a) 新規ページで最小scaleへ
  // (a) Open a fresh page and zoom to the minimum scale
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });
  await openResearchTree(page);

  const center = { x: VIEWPORT.width / 2, y: VIEWPORT.height / 2 };
  await page.mouse.move(center.x, center.y);
  // MIN_VIEW_SCALE(0.4)に張り付くまで大きく倒す一発のwheel（viewport.tsのクランプに依存）
  // One large wheel event pegged to MIN_VIEW_SCALE(0.4); relies on the clamp in viewport.ts
  await page.mouse.wheel(0, 2000);
  const researchableNode = page.getByTestId(`research-node-${researchableNodeGuid}`);
  await researchableNode.waitFor();

  // ズーム直後は既定中央寄せのため一部ノードが持ち物パネルの右隣パネル外へはみ出す。
  // 空白背景をドラッグして4ノードを見える位置へ動かす（(450,600)→+556.5,-63.5は実測起点）
  // Right after the zoom, default centering places some nodes outside the panel next to the inventory;
  // drag the empty background to bring all 4 nodes into view ((450,600)→+556.5,-63.5 is a measured anchor)
  // 離す直前で静止する（PAN_RELEASE_STALL_MSより長く止めて慣性オーバーシュートを防ぐ）
  // Hold still right before release (longer than PAN_RELEASE_STALL_MS) to avoid a fling-momentum overshoot
  await page.mouse.move(450, 600);
  await page.mouse.down();
  await page.mouse.move(450 + 556.5, 600 - 63.5, { steps: 30 });
  await page.waitForTimeout(150);
  await page.mouse.up();
  await settleBoundingBox(page, researchableNode);
  await shoot(page, "overview-full.png");

  await page.close();
}

async function captureWideOverview(browser: Awaited<ReturnType<typeof chromium.launch>>) {
  // (d) 持ち物右隣パネルの左右端確認
  // (d) Check the panel-next-to-inventory's left/right edges
  const page = await browser.newPage({ viewport: WIDE_VIEWPORT, deviceScaleFactor: 2 });
  await openResearchTree(page);
  await shoot(page, "overview-wide.png");
  await page.close();
}

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  await captureDetailPanes(browser);
  await captureOverview(browser);
  await captureWideOverview(browser);
  await browser.close();

  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
