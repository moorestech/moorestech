// 列車インベントリの「インベントリ / 時刻表」タブ切替をPRレビュー用に撮影する
// Captures the train inventory's inventory/timetable tab switch for PR review

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5412);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/train-timetable-qa";
const VIEWPORT = { width: 1280, height: 720 } as const;

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
  const context = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 2 });
  const page = await context.newPage();

  await page.request.get(`http://127.0.0.1:${PORT}/__block?type=trainTimetable`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("block-inventory").waitFor();
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);

  // インベントリタブ（初期表示）
  // Inventory tab (initial view)
  await page.screenshot({ path: join(OUT_DIR, "train-tab-inventory.png") });

  // 時刻表タブへ切替
  // Switch to the timetable tab
  await page.getByTestId("train-tab-timetable").click();
  await page.getByTestId("train-timetable-section").waitFor();
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(OUT_DIR, "train-tab-timetable.png") });

  await page.close();
  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
