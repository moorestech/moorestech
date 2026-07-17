// 採点用スクショハーネス: mock-host(DEMO) を起動し、インベントリ/クラフト画面を正本と同寸(2568x1450)で撮影する
// Scoring screenshot harness: boot mock-host (DEMO) and capture the inventory/craft screen at the reference size (2568x1450)

import { chromium } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5399);
const OUT = process.env.CAPTURE_OUT ?? "turn-shot.png";
const CROP_DIR = process.env.CAPTURE_CROP_DIR;

// 明背景の注入有無を切替える（透過採点は明背景、素の見た目確認は無地）
// Toggle the bright-background injection (bright for translucency scoring, none for a plain look-check)
const INJECT_BG = process.env.CAPTURE_BG !== "0";

async function main() {
  // DEMO は mock-host の module ロード時に評価される。ESM の import 巻き上げを避けるため env 設定後に動的 import する
  // DEMO is read at mock-host module-load; set env first then dynamic-import to dodge ESM import hoisting
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");

  // mock-host を単体起動（playwright config を介さず capture 専用に束ねる）
  // Boot mock-host standalone (bundled for capture, bypassing the playwright config)
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));

  const browser = await chromium.launch();
  // viewport 1284x725 × dSF2 = 2568x1450（正本と同寸）
  // viewport 1284x725 × dSF2 = 2568x1450 (same size as the reference)
  const context = await browser.newContext({ viewport: { width: 1284, height: 725 }, deviceScaleFactor: 2 });
  const page = await context.newPage();

  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByRole("heading", { name: "CRAFT RECIPE" }).waitFor();

  // レシピを1件選択して選択枠を表示させる（状態パリティの要件）
  // Select one recipe so the selection frame is visible (state-parity requirement)
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await page.getByTestId("craft-recipe-box").waitFor();

  // 明るい世界風背景を注入し、パネル越しの透過・視認性を判定可能にする
  // body は position:fixed の viewport のみで高さ0のため、背景は全画面固定 DIV を UI 背後(z-index:-1)へ挿入する
  // The body has zero height (only the fixed viewport lives in it), so inject a full-screen fixed DIV behind the UI (z-index:-1)
  if (INJECT_BG) {
    await page.evaluate(() => {
      const bg = document.createElement("div");
      bg.id = "__worldbg";
      bg.style.cssText =
        "position:fixed;inset:0;z-index:-1;pointer-events:none;background:linear-gradient(180deg,#7bb054 0%,#6fa04a 42%,#855a3f 52%,#9b6a4a 62%,#8a5c40 100%);";
      document.body.appendChild(bg);
    });
  }

  // ホバー由来のツールチップを消すためカーソルを画面外へ退避する
  // Move the cursor off-screen so hover tooltips do not appear in the shot
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await page.screenshot({ path: OUT });

  // 等倍クロップで装飾を再検査する
  // Recheck ornaments with equal-scale crops
  if (CROP_DIR !== undefined) {
    await mkdir(CROP_DIR, { recursive: true });
    const recipeBox = page.getByTestId("craft-recipe-box");
    const craftPanel = recipeBox.locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
    const inventoryPanel = page.getByRole("heading", { name: "持ち物" }).locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
    const recipePanel = page.getByRole("heading", { name: "CRAFT RECIPE" }).locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
    const selectedSlot = page.getByTestId("item-list-grid").locator("[data-selected='true']");

    await inventoryPanel.screenshot({ path: join(CROP_DIR, "inventory-panel.png") });
    await recipePanel.screenshot({ path: join(CROP_DIR, "recipe-list-panel.png") });
    await craftPanel.screenshot({ path: join(CROP_DIR, "craft-panel.png") });
    await recipeBox.screenshot({ path: join(CROP_DIR, "recipe-selection.png") });
    await page.getByTestId("recipe-divider-ornament").screenshot({ path: join(CROP_DIR, "divider-ornament.png") });
    await page.getByTestId("hotbar-grid").screenshot({ path: join(CROP_DIR, "hotbar.png") });
    await selectedSlot.screenshot({ path: join(CROP_DIR, "selected-slot.png") });
  }

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  // 明示的に終了（WS の open handle が残るのを避ける）
  // Exit explicitly to avoid lingering WS open handles
  process.exit(0);
}

void main();
