// 接続線関連の通知トーストを実描画で撮影する
// Capture connection-line notification toasts as real renders

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";
import { Topics } from "../../src/bridge/transport/protocol";

const PORT = Number(process.env.CAPTURE_PORT ?? 5413);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/connection-line-notifications";
const MESSAGE_IDS = [
  "denied.undoRestoreSkipped",
  "denied.gearChainDisconnect.InventoryFull",
  "denied.gearChainConnect.TooFar",
  "denied.gearChainConnect.ConnectionLimit",
  "denied.gearChainConnect.NoItem",
];

async function main() {
  // mock hostを専用ポートで起動
  // Boot the mock host on a dedicated port
  const { createMockHttpServer } = await import("../mock-host/httpHandler");
  const { attachWsHandlers } = await import("../mock-host/wsHandler");
  const { topicSubscribers } = await import("../mock-host/state");
  const { send } = await import("../mock-host/wire");
  const { applyLocalizationAction } = await import("../mock-host/localization/transport");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 2 });
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=GameScreen&subState=GameScreen`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  // 各idをnotification topicへ直接pushする
  // Push each id straight onto the notification topic
  const pushAll = async () => {
    for (const [i, messageId] of MESSAGE_IDS.entries()) {
      const data = { seq: 100 + i, category: "operationDenied" as const, messageId, messageParams: ["3"], itemId: null };
      for (const ws of topicSubscribers.get(Topics.notification) ?? []) send(ws, { op: "event", topic: Topics.notification, data });
    }
    await page.getByTestId("notification-row").nth(MESSAGE_IDS.length - 1).waitFor();
    await page.waitForTimeout(700);
  };
  await pushAll();
  await page.screenshot({ path: join(OUT_DIR, "notifications-ja.png") });

  // 英語へ切り替えて再撮影する
  // Switch to English and shoot again
  await page.waitForTimeout(8500);
  applyLocalizationAction("localization.setLocale", { locale: "english" });
  await page.waitForTimeout(500);
  await pushAll();
  await page.screenshot({ path: join(OUT_DIR, "notifications-en.png") });

  await browser.close();
  server.close();
  wss.close();
}

main().then(() => process.exit(0), (error) => { console.error(error); process.exit(1); });
