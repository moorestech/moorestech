// 採点用スクショハーネス: mock-host(DEMO) を起動し、インベントリ/クラフト画面を正本と同寸(2568x1450)で撮影する
// Scoring screenshot harness: boot mock-host (DEMO) and capture the inventory/craft screen at the reference size (2568x1450)

import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5399);
const OUT = process.env.CAPTURE_OUT ?? "turn-shot.png";

// 正本スクショと同寸で撮るためviewportをenvで可変にする
// Make the viewport env-configurable to match reference screenshots of any size
const VIEWPORT_W = Number(process.env.CAPTURE_VIEWPORT_W ?? 1284);
const VIEWPORT_H = Number(process.env.CAPTURE_VIEWPORT_H ?? 725);

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
  // viewport × dSF2 が正本と同寸になるよう指定する（例: 1635x922 → 3270x1844）
  // Pick a viewport whose ×2 dSF size equals the reference (e.g. 1635x922 → 3270x1844)
  const context = await browser.newContext({ viewport: { width: VIEWPORT_W, height: VIEWPORT_H }, deviceScaleFactor: 2 });
  const page = await context.newPage();

  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByRole("heading", { name: "CRAFT RECIPE" }).waitFor();

  // レシピを1件選択して選択枠を表示させる（状態パリティの要件）
  // Select one recipe so the selection frame is visible (state-parity requirement)
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await page.locator('[class*="_recipeBox_"]').waitFor();

  // Webフォントのロード完了を待ち、主要要素の実効フォントを記録する（フォント未適用の撮影を防ぐ）
  // Wait for web fonts and log effective fonts on key elements (prevents capturing with fallback fonts)
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  const fontReport = await page.evaluate(`(() => {
    const faces = [];
    document.fonts.forEach((f) => faces.push(f.family + ":" + f.status));
    const pick = (sel) => {
      const el = document.querySelector(sel);
      return el ? getComputedStyle(el).fontFamily.slice(0, 60) : "(none)";
    };
    return JSON.stringify({
      loaded: faces.join(", "),
      body: getComputedStyle(document.body).fontFamily.slice(0, 60),
      heading: pick("h1,h2,h3,h4"),
      count: pick('[class*="count"]'),
      button: pick("button"),
    });
  })()`);
  console.log("fonts:", fontReport);

  // 素の確認時だけ共用背景を除く
  // Remove the shared mock-host background only for the plain look-check
  if (!INJECT_BG) {
    await page.locator("#__worldbg").evaluate((background) => background.remove());
  }

  // ホバー由来のツールチップを消すためカーソルを画面外へ退避する
  // Move the cursor off-screen so hover tooltips do not appear in the shot
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await page.screenshot({ path: OUT });

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  // 明示的に終了（WS の open handle が残るのを避ける）
  // Exit explicitly to avoid lingering WS open handles
  process.exit(0);
}

void main();
