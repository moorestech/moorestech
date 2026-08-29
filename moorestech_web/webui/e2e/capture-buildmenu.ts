// 目視QA:5状態を撮影
// Visual QA: capture 5 BuildMenu states

import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";
// fixtures は型と定数のみでMOCK_DEMOを読まないため、静的importしてよい
// fixtures holds only types and constants and never reads MOCK_DEMO, so a static import is safe
import {
  buildMenuCategoryIds,
  buildMenuEntryIds,
  buildMenuSubCategoryIds,
} from "./mock-host/fixtures";

const PORT = Number(process.env.CAPTURE_PORT ?? 5401);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? ".";
const VIEWPORT_W = Number(process.env.CAPTURE_VIEWPORT_W ?? 1284);
const VIEWPORT_H = Number(process.env.CAPTURE_VIEWPORT_H ?? 725);

async function main() {
  // DEMO は mock-host の module ロード時に評価される。env 設定後に動的 import する
  // DEMO is read at mock-host module-load; set env first then dynamic-import
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");

  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));

  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: VIEWPORT_W, height: VIEWPORT_H }, deviceScaleFactor: 2 });
  const page = await context.newPage();

  // BuildMenu の uiState を先に立ててから読み込む（購読時snapshotに載る）
  // Set BuildMenu uiState before load so it rides the subscribe snapshot
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=BuildMenu`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("build-menu-panel").waitFor();
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  // 1. 既定表示（先頭カテゴリが視口上端・カーソル退避）
  // 1. Default view (first category at viewport top, cursor parked off-screen)
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${OUT_DIR}/buildmenu-1-default.png` });

  // 2.検索中(絞り込み)
  // 2. Searching (filtered)
  await page.getByTestId("build-menu-search").fill("鉄");
  await page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.transport}-${buildMenuSubCategoryIds.rail}`,
  ).waitFor();
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${OUT_DIR}/buildmenu-2-search.png` });

  // 3.ホバー(検索クリア後)
  // 3. Hover (after clearing search)
  await page.getByTestId("build-menu-search").fill("");
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`).hover();
  await page.getByTestId("build-menu-detail").waitFor();
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${OUT_DIR}/buildmenu-3-hover.png` });

  // 4.8列グリッドが埋まるカテゴリ
  // 4. Fills all 8 cols
  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`).waitFor();
  // スムーズスクロール完了待ち
  // Wait for smooth scrolling to settle
  await page.waitForTimeout(600);
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${OUT_DIR}/buildmenu-4-grid.png` });

  // 5.英語ロケール撮影
  // 5. English locale screenshot
  await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=english`);
  await page.waitForFunction(() => document.documentElement.dataset.locale === "english");
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${OUT_DIR}/buildmenu-5-english.png` });

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);
}

void main();
