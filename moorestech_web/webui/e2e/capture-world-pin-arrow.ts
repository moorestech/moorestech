// 画面外ワールドピン矢印を背景と方向を変えて撮影する
// Capture the off-screen world-pin arrow across varied backgrounds and directions

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5402);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/world-pin-arrow-qa";
const ARROW_TEST_ID = "world-pin-arrow-map-object-pin";

const captureCases = [
  {
    name: "world-pin-arrow-right-light.png",
    directionX: 1,
    directionY: 0,
    background: "repeating-linear-gradient(45deg,#f5f0df 0 12px,#b6c6aa 12px 24px)",
  },
  {
    name: "world-pin-arrow-left-up-dark.png",
    directionX: -0.4,
    directionY: -0.9,
    background: "radial-gradient(circle at 30% 40%,#3e4f38 0 8%,transparent 9%),repeating-linear-gradient(135deg,#101722 0 10px,#293020 10px 20px)",
  },
  {
    name: "world-pin-arrow-right-down-game.png",
    directionX: 0.8,
    directionY: 0.6,
    background: "url('/mock-orange-gradient.png') center/cover no-repeat",
  },
] as const;

async function main() {
  // DEMO背景を含むmock hostをcapture専用ポートで起動する
  // Boot the mock host with its demo background on the capture-only port
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  await mkdir(OUT_DIR, { recursive: true });

  // 実際のHUD比率でWeb UIを開き、購読開始を待ってから矢印を配信する
  // Open the Web UI at the real HUD aspect ratio and wait for subscriptions before publishing arrows
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=GameScreen&subState=GameScreen`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("hotbar-grid").waitFor();

  // 各ケースで方向更新を待ち、背景を差し替えて全viewportを撮影する
  // Wait for each direction update, swap the background, and capture the full viewport
  for (const capture of captureCases) {
    await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?on=0&dx=${capture.directionX}&dy=${capture.directionY}&text=TutorialTarget`);
    const arrow = page.getByTestId(ARROW_TEST_ID);
    await arrow.waitFor();
    await waitForDirection(capture.directionX, capture.directionY);
    await page.locator("#__worldbg").evaluate((element, background) => {
      (element as HTMLElement).style.background = background;
    }, capture.background);
    await page.screenshot({ path: join(OUT_DIR, capture.name) });
  }

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);

  async function waitForDirection(directionX: number, directionY: number) {
    const expectedAngle = (Math.atan2(directionY, directionX) * 180) / Math.PI;
    await page.waitForFunction(({ testId, angle }) => {
      const arrow = document.querySelector(`[data-testid="${testId}"]`);
      const match = arrow?.getAttribute("style")?.match(/rotate\((-?[\d.]+)deg\)/);
      return match !== undefined && match !== null && Math.abs(Number(match[1]) - angle) < 0.01;
    }, { testId: ARROW_TEST_ID, angle: expectedAngle });
  }
}

void main();
