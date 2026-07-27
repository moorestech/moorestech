// 目視QA: IconButton化した×ボタンの見た目とcomputed styleを確認する
// Visual QA: check the IconButton-based close buttons' look and computed styles

import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5403);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? ".";

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");

  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));

  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1284, height: 725 }, deviceScaleFactor: 2 });
  const page = await context.newPage();
  const control = (path: string) => page.request.get(`http://127.0.0.1:${PORT}${path}`);

  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");

  // ボタン枠とアイコンの実寸・色を読み、旧PanelCloseButtonの定数（--slot-size/2・/3・--text-muted）と一致するか確かめる
  // Read the button and icon box and color to confirm they still equal the old PanelCloseButton constants
  const report = async (testId: string) => {
    const button = page.getByTestId(testId);
    const box = await button.boundingBox();
    const styles = await button.evaluate((element) => {
      const svg = element.querySelector("svg") as SVGElement;
      return {
        color: getComputedStyle(element).color,
        svgWidth: getComputedStyle(svg).width,
        svgHeight: getComputedStyle(svg).height,
      };
    });
    return { testId, width: box?.width, height: box?.height, ...styles };
  };

  await control("/__uistate?state=SubInventory");
  await control("/__block?type=chest");
  await page.getByTestId("block-inventory-close").waitFor();
  await page.screenshot({ path: `${OUT_DIR}/iconbutton-1-block-inventory.png` });
  const blockReport = await report("block-inventory-close");

  await control("/__uistate?state=BuildMenu");
  await page.getByTestId("build-menu-close").waitFor();
  await page.screenshot({ path: `${OUT_DIR}/iconbutton-2-build-menu.png` });
  const buildReport = await report("build-menu-close");

  console.log(JSON.stringify([blockReport, buildReport], null, 2));

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  process.exit(0);
}

void main();
