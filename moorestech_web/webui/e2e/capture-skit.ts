// 目視QA: スキット会話UIの各状態を撮影
// Visual QA: capture the skit dialogue UI in each state

import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5402);
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
  const control = (path: string) => page.request.get(`http://127.0.0.1:${PORT}${path}`);

  // スキットはゲームプレイ中に出るため GameScreen を土台にする（持ち物パネルを重ねない）
  // Skits appear during gameplay, so base every shot on GameScreen without the inventory on top
  await control("/__uistate?state=GameScreen");
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  // mock-host は notification.events の snapshot を返さないため、購読が先着すると restoring が解けず
  // 再接続オーバーレイが撮影とクリックを塞ぐ。撮影用にそれだけ落とす（既存ハーネスの穴で本UIとは無関係）
  // The mock host serves no notification.events snapshot, so when the subscribe wins the race the restore never
  // finishes and the reconnect overlay blocks shots and clicks; drop just that overlay for capture purposes
  await page.addStyleTag({ content: '[data-testid="reconnect-overlay"] { display: none !important; }' });

  const shoot = async (name: string) => {
    await page.mouse.move(2, 2);
    await page.waitForTimeout(300);
    await page.screenshot({ path: `${OUT_DIR}/${name}.png` });
  };

  // 1.背景スキット（面なし1行）
  // 1. Background skit (faceless single line)
  await control("/__skit?stage=background");
  await page.getByTestId("background-skit").waitFor();
  await shoot("skit-1-background");

  // 2.通常スキット本文。§10の端チェック用に会話窓の左右も拡大クロップする
  // 2. Blocking body text, plus zoomed left/right window crops for the §10 edge check
  await control("/__skit?stage=text");
  await page.getByTestId("blocking-skit").waitFor();
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

  // 5.UI非表示（Hide UI 押下で復帰アイコンだけが残る）
  // 5. UI hidden (pressing Hide UI leaves only the restore icon)
  await control("/__skit?stage=text");
  await page.getByTestId("blocking-skit").waitFor();
  await page.getByRole("button", { name: "Hide UI" }).click();
  await page.getByTestId("skit-show-ui").waitFor();
  await shoot("skit-5-ui-hidden");

  // 6.Auto ON（同一SVGの色替えだけで表す）
  // 6. Auto ON (expressed purely as a color swap on the same SVG)
  await control("/__skit?stage=text");
  await page.getByTestId("blocking-skit").waitFor();
  await page.getByRole("button", { name: "Auto" }).click();
  await page.waitForTimeout(200);
  await shoot("skit-6-auto-on");

  // 7.暗転が会話窓より上に載ることを確認する（stage合わせのfixtureが無いのでtopicを直接押し込む）
  // 7. Verify the blackout paints above the window (no staged fixture exists, so push the topic directly)
  await control("/__skit?stage=text");
  await page.getByTestId("blocking-skit").waitFor();
  const { state, subscribersOf } = await import("./mock-host/state");
  const { send } = await import("./mock-host/wire");
  const { Topics } = await import("../src/bridge/transport/protocol");
  state.skitPresentation = {
    ...state.skitPresentation,
    sceneRevision: state.skitPresentation.sceneRevision + 1,
    presentationState: { ...state.skitPresentation.presentationState, transitionVisible: true },
  };
  for (const ws of subscribersOf(Topics.skitPresentation)) {
    send(ws, { op: "event", topic: Topics.skitPresentation, data: state.skitPresentation });
  }
  await page.getByTestId("skit-transition").waitFor();
  await shoot("skit-7-transition");

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);
}

void main();
