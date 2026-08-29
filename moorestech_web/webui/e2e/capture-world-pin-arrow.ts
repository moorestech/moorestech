// 画面外矢印の視覚QA
// Capture the off-screen world-pin arrow across varied backgrounds and directions

import { createHash } from "node:crypto";
import { readFile, mkdir, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";
import { freezeAttentionPulse } from "./support/pulseFreeze";

const PORT = Number(process.env.CAPTURE_PORT ?? 5402);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/world-pin-arrow-qa";
const ARROW_TEST_ID = "world-pin-arrow-map-object-pin";
const DIRECTION_ANGLE_TOLERANCE_DEGREES = 0.01;
const VIEWPORT = { width: 1280, height: 720 } as const;

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
  // capture用mock hostを起動
  // Boot the mock host with its demo background on the capture-only port
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  await mkdir(OUT_DIR, { recursive: true });

  // 旧manifestを先に無効化
  // Invalidate any stale manifest before capture
  await rm(join(OUT_DIR, "manifest.json"), { force: true });

  // HUD比率で購読開始を待つ
  // Open the Web UI at the real HUD aspect ratio and wait for subscriptions before publishing arrows
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT });
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=GameScreen&subState=GameScreen`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("hotbar-grid").waitFor();

  // 方向と背景を変えて撮影
  // Wait for each direction update, swap the background, and capture the full viewport
  for (const capture of captureCases) {
    await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?on=0&dx=${capture.directionX}&dy=${capture.directionY}&text=TutorialTarget`);
    const arrow = page.getByTestId(ARROW_TEST_ID);
    await arrow.waitFor();
    await waitForDirection(capture.directionX, capture.directionY);
    await page.locator("#__worldbg").evaluate((element, background) => {
      (element as HTMLElement).style.background = background;
    }, capture.background);
    await freezeAttentionPulse(page);
    await page.screenshot({ path: join(OUT_DIR, capture.name) });
  }

  // 全画像の生成後だけhash付きmanifestを残し、古い途中結果との混同を防ぐ
  // Write a hashed manifest only after every image exists to distinguish complete runs from stale partial output
  const captures = await Promise.all(captureCases.map(async (capture) => {
    const image = await readFile(join(OUT_DIR, capture.name));
    return {
      ...capture,
      width: VIEWPORT.width,
      height: VIEWPORT.height,
      sha256: createHash("sha256").update(image).digest("hex"),
    };
  }));
  await writeFile(join(OUT_DIR, "manifest.json"), `${JSON.stringify({
    generatedAt: new Date().toISOString(),
    captures,
  }, null, 2)}\n`);

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);

  async function waitForDirection(directionX: number, directionY: number) {
    const expectedAngle = (Math.atan2(directionY, directionX) * 180) / Math.PI;
    await page.waitForFunction(({ testId, angle, tolerance }) => {
      const arrow = document.querySelector(`[data-testid="${testId}"]`);
      const match = arrow?.getAttribute("style")?.match(/rotate\((-?[\d.]+)deg\)/);
      return match !== undefined
        && match !== null
        && Math.abs(Number(match[1]) - angle) < tolerance;
    }, {
      testId: ARROW_TEST_ID,
      angle: expectedAngle,
      tolerance: DIRECTION_ANGLE_TOLERANCE_DEGREES,
    });
  }
}

void main();
