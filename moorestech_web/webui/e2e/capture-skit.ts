// 目視QA: スキットUI状態を撮影
// Visual QA: capture the skit UI states

import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5402);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? ".";
const VIEWPORT_W = Number(process.env.CAPTURE_VIEWPORT_W ?? 1284);
const VIEWPORT_H = Number(process.env.CAPTURE_VIEWPORT_H ?? 725);
// 全文表示判定用の本文定数
// Body constant used to detect a completed reveal
const BLOCKING_BODY = "Blocking message";

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
  const control = (path: string) => page.request.get(`http://127.0.0.1:${PORT}${path}`);

  // スキットはゲームプレイ中に出るため GameScreen を土台にする（持ち物パネルを重ねない）
  // Skits appear during gameplay, so base every shot on GameScreen without the inventory on top
  await control("/__uistate?state=GameScreen");
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  const shoot = async (name: string) => {
    await page.mouse.move(2, 2);
    await page.waitForTimeout(300);
    await page.screenshot({ path: `${OUT_DIR}/${name}.png` });
  };

  // blocking fixture は intervalMs=1000 のタイプライターで全文まで十数秒かかるため、実ユーザー同様クリックで即時全表示させる
  // The blocking fixture types at intervalMs=1000 and needs a dozen seconds, so a real-user click reveals it all at once
  const showBlockingBody = async () => {
    const skitWindow = page.getByTestId("blocking-skit");
    await skitWindow.waitFor();
    for (let attempt = 0; attempt < 20; attempt += 1) {
      // 同一台詞の再送ではタイプライターが再開せず全文のまま。その状態のクリックはadvanceとなり次の場面へ進むため必ず先に判定する
      // A resent identical line keeps the full body, and clicking then dispatches advance and jumps scenes, so always check first
      if ((await skitWindow.textContent())?.includes(BLOCKING_BODY)) return;
      await skitWindow.click();
      await page.waitForTimeout(100);
    }
    throw new Error(`blocking body never completed: ${BLOCKING_BODY}`);
  };

  // 1.背景スキット（面なし1行）
  // 1. Background skit (faceless single line)
  await control("/__skit?stage=background");
  await page.getByTestId("background-skit").waitFor();
  await shoot("skit-1-background");

  // 2.通常スキット本文＋左右クロップ
  // 2. Blocking body text plus left/right crops
  await control("/__skit?stage=text");
  await showBlockingBody();
  await shoot("skit-2-text");
  await page.screenshot({ path: `${OUT_DIR}/skit-2a-window-left.png`, clip: { x: 0, y: 495, width: 430, height: 225 } });
  await page.screenshot({ path: `${OUT_DIR}/skit-2b-window-right.png`, clip: { x: 850, y: 495, width: 430, height: 225 } });

  // 3.選択肢
  // 3. Choices
  await control("/__skit?stage=choices");
  await page.getByRole("button", { name: "Route B" }).waitFor();
  await shoot("skit-3-choices");
  await page.screenshot({ path: `${OUT_DIR}/skit-3a-choices-crop.png`, clip: { x: 1080, y: 405, width: 200, height: 110 } });

  // 4.選択肢ホバー（線と菱形のシアン切替）
  // 4. Choice hover (cyan swap on the rules and diamonds)
  await page.getByRole("button", { name: "Route B" }).hover();
  await page.waitForTimeout(300);
  await page.screenshot({ path: `${OUT_DIR}/skit-4-choice-hover.png` });

  // 5.UI非表示（復帰アイコンのみ）
  // 5. UI hidden (restore icon only)
  await control("/__skit?stage=text");
  await showBlockingBody();
  await page.getByRole("button", { name: "Hide UI" }).click();
  await page.getByTestId("skit-show-ui").waitFor();
  await shoot("skit-5-ui-hidden");

  // 6.Auto ON（色替えのみで表現）
  // 6. Auto ON (expressed by a color swap alone)
  await control("/__skit?stage=text");
  await showBlockingBody();
  await page.getByRole("button", { name: "Auto" }).click();
  await page.waitForTimeout(200);
  await shoot("skit-6-auto-on");

  // 7.暗転が会話窓より上か確認
  // 7. Verify the blackout sits above the window
  await control("/__skit?stage=transition");
  await page.getByTestId("skit-transition").waitFor();
  await shoot("skit-7-transition");

  // 8.会話窓非表示の演出中もツールバーが右上に残る
  // 8. The toolbar stays in the top-right while the window is hidden during staging
  await control("/__skit?stage=staging");
  await page.getByRole("button", { name: "Auto" }).waitFor();
  await page.getByTestId("blocking-skit").waitFor({ state: "detached" });
  await shoot("skit-8-staging");

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);
}

void main();
