// 目視QA: 採掘HUD整列を撮影
// Visual QA: capture mining HUD alignment

import { createHash } from "node:crypto";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5403);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/mining-progress-hud";
const VIEWPORT = { width: 1280, height: 720 } as const;
const CROP_TOP_PADDING_PX = 48;
const CROP_HORIZONTAL_PADDING_PX = 64;

async function main() {
  // 背景付きmock hostを専用起動
  // Boot the background mock host on its capture port
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);
  await mkdir(OUT_DIR, { recursive: true });
  await rm(join(OUT_DIR, "manifest.json"), { force: true });

  // 採掘状態を先に設定してから実HUD比率で購読を開始する
  // Set mining state before subscribing at the real HUD aspect ratio
  const browser = await chromium.launch();
  const page = await browser.newPage({
    viewport: VIEWPORT,
    deviceScaleFactor: 2,
  });
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=GameScreen&subState=GameScreen`);
  await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=mining`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  // HUD要素が描画された座標と色を画像と同じ時点で実測する
  // Measure HUD geometry and colors at the exact moment used for screenshots
  const progress = page.getByTestId("progress-bar");
  const gauge = page.getByTestId("progress-gauge");
  const hotbar = page.getByTestId("hotbar-grid");
  const firstNumberTab = hotbar.locator("> div").first().locator("> span").first();
  await progress.waitFor();
  await hotbar.waitFor();
  const [progressBox, hotbarBox, numberTabBox] = await Promise.all([
    progress.boundingBox(),
    hotbar.boundingBox(),
    firstNumberTab.boundingBox(),
  ]);
  if (progressBox === null || hotbarBox === null || numberTabBox === null) {
    throw new Error("mining progress capture boxes were not rendered");
  }

  // 全景とHUDクロップで視認性確認
  // Use full and HUD crops to check legibility
  const fullName = "full.png";
  const cropName = "hotbar-crop.png";
  await page.mouse.move(2, 2);
  await page.screenshot({ path: join(OUT_DIR, fullName) });
  const cropTop = Math.max(0, progressBox.y - CROP_TOP_PADDING_PX);
  await page.screenshot({
    path: join(OUT_DIR, cropName),
    clip: {
      x: Math.max(0, hotbarBox.x - CROP_HORIZONTAL_PADDING_PX),
      y: cropTop,
      width: Math.min(VIEWPORT.width, hotbarBox.width + CROP_HORIZONTAL_PADDING_PX * 2),
      height: VIEWPORT.height - cropTop,
    },
  });

  // 画像hashと実測値を揃え、古い証跡や目測だけの合格を防ぐ
  // Pair image hashes with measurements to reject stale evidence and eyeballing alone
  const [fullImage, cropImage, gaugeColors] = await Promise.all([
    readFile(join(OUT_DIR, fullName)),
    readFile(join(OUT_DIR, cropName)),
    gauge.evaluate((element) => ({
      track: getComputedStyle(element).backgroundColor,
      fill: getComputedStyle(element.firstElementChild as Element).backgroundColor,
    })),
  ]);
  const progressCenter = progressBox.x + progressBox.width / 2;
  const hotbarCenter = hotbarBox.x + hotbarBox.width / 2;
  await writeFile(join(OUT_DIR, "manifest.json"), `${JSON.stringify({
    generatedAt: new Date().toISOString(),
    viewport: VIEWPORT,
    progressBox,
    hotbarBox,
    numberTabBox,
    widthDifference: Math.abs(progressBox.width - hotbarBox.width),
    centerDifference: Math.abs(progressCenter - hotbarCenter),
    numberTabGap: numberTabBox.y - (progressBox.y + progressBox.height),
    progressbarCount: await page.getByRole("progressbar").count(),
    miningTargetTextCount: await page.getByText(/Mining Target/i).count(),
    gaugeColors,
    captures: [
      { name: fullName, sha256: createHash("sha256").update(fullImage).digest("hex") },
      { name: cropName, sha256: createHash("sha256").update(cropImage).digest("hex") },
    ],
  }, null, 2)}\n`);

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
