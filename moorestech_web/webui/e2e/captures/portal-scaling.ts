// Portal層(ツールチップ・通知・ワールドピン)の見かけの大きさを解像度ごとに撮る視覚QA
// Visual QA capturing the apparent size of the portal layers (tooltip, notification, world pin) per resolution

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5403);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/portal-scaling-qa";

// このMacのフル解像度と、比較対象の小さい窓
// This Mac's full resolution plus the smaller window it is compared against
const VIEWPORTS = [
  { name: "3024x1964", width: 3024, height: 1964 },
  { name: "1600x800", width: 1600, height: 800 },
] as const;

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("../mock-host/httpHandler");
  const { attachWsHandlers } = await import("../mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  for (const viewport of VIEWPORTS) {
    const page = await browser.newPage({ viewport: { width: viewport.width, height: viewport.height } });
    await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=GameScreen&subState=GameScreen`);
    await page.goto(`http://127.0.0.1:${PORT}/`);
    await page.getByTestId("hotbar-grid").waitFor();

    await capture(page, viewport.name, "tooltip", async () => {
      await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=tooltipMultiLine`);
      await page.mouse.move(viewport.width * 0.42, viewport.height * 0.4);
      await page.getByTestId("cursor-tooltip").waitFor();
    });
    await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=tooltipHidden`);

    // 通知は数秒で退場するため、撮影中だけ出入りアニメを止めて実寸を写す
    // Notifications exit within seconds, so the enter/exit animation is frozen for the capture to show their real size
    await page.addStyleTag({ content: '[data-testid="notification-row"] { animation: none !important; opacity: 1 !important; }' });
    await capture(page, viewport.name, "notification", async () => {
      await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=notificationItemEarned`);
      await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=notificationDenied`);
      await page.getByTestId("notification-row").first().waitFor();
    });
    await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=notificationClear`);

    await capture(page, viewport.name, "world-pin", async () => {
      await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?x=0.45&y=0.45`);
      await page.getByTestId("world-pin-map-object-pin").waitFor();
    });

    await capture(page, viewport.name, "tutorial-outline-label", async () => {
      await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=tutorialOutlineWithLabel`);
      await page.getByTestId("tutorial-highlight-label").waitFor();
    });
    await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=tutorialEmpty`);

    await capture(page, viewport.name, "world-pin-edge-arrow", async () => {
      await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?on=0&dx=1&dy=-0.35`);
      await page.getByTestId("world-pin-arrow-map-object-pin").waitFor();
    });
    await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?clear=1`);

    await page.close();
  }

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);

  async function capture(page: Page, viewportName: string, subject: string, arrange: () => Promise<void>) {
    await arrange();
    await page.screenshot({ path: join(OUT_DIR, `${subject}--${viewportName}.png`) });
  }
}

void main();
