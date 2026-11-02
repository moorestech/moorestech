// 研究詳細ペインのスクロール目視QA撮影（解放物が極端に多いノード）
// Visual QA capture for the research detail pane scroll (a node with a huge number of unlocks)

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";
import { WebSocketServer } from "ws";
import { researchTree, researchableNodeGuid, itemLackingNodeGuid } from "./mock-host/researchFixtures";

const PORT = Number(process.env.CAPTURE_PORT ?? 5413);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/research-detail-scroll";
const VIEWPORT = { width: 1280, height: 720 } as const;
const HUGE_UNLOCK_COUNT = 60;

// 研究可能ノードの解放ブロック・機械レシピを大量に水増しする（/__research がcloneする前に書き換える）
// Inflate the researchable node's unlocked blocks and machine recipes before /__research clones the fixture
function inflateResearchableNode() {
  const target = researchTree.nodes.find((n) => n.guid === researchableNodeGuid);
  if (!target) throw new Error("researchable fixture node missing");
  const block = target.unlockBlocks[0];
  const recipe = target.unlockMachineRecipes[0];
  target.unlockBlocks = Array.from({ length: HUGE_UNLOCK_COUNT }, () => ({ ...block }));
  target.unlockMachineRecipes = Array.from({ length: HUGE_UNLOCK_COUNT }, () => ({ ...recipe }));
}

async function openResearchTree(page: Page) {
  // mock制御はcontextにbaseURLが無いため絶対URLで叩く（capture-research-qa.ts踏襲）
  // No context baseURL is set, so hit mock control with absolute URLs (follows capture-research-qa.ts)
  await page.request.get(`http://127.0.0.1:${PORT}/__research`);
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=ResearchTree`);
  await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=researchOwnedItems`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("research-tree").waitFor();
  await page.evaluate("document.fonts.ready.then(() => undefined)");
}

async function shoot(page: Page, name: string) {
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(OUT_DIR, name) });
}

async function captureHugeAndSmallPanes(page: Page) {
  // 巨大ノード: 開いた直後とホイール後を撮り、ペインと研究ボタンの矩形を記録する
  // Huge node: shoot before and after wheeling, and log the pane and research-button boxes
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const pane = page.getByTestId("research-detail-pane");
  await pane.waitFor();
  await page.mouse.move(2, 2);
  await shoot(page, "huge-top.png");
  const scroll = page.getByTestId("research-detail-scroll");
  const scrollBox = await scroll.boundingBox();
  if (!scrollBox) throw new Error("scroll area not rendered");
  await page.mouse.move(scrollBox.x + scrollBox.width / 2, scrollBox.y + scrollBox.height / 2);
  await page.mouse.wheel(0, 100000);
  await shoot(page, "huge-bottom.png");
  const button = page.getByTestId(`research-button-${researchableNodeGuid}`);
  console.log(JSON.stringify({ viewport: VIEWPORT, huge: { pane: await pane.boundingBox(), button: await button.boundingBox() } }));

  // 小さいノード: 中身なりの高さに戻り、偽の溢れ(スクロールバー)が出ないことを数値でも確かめる
  // Small node: the pane shrinks to its content and the viewport reports no phantom overflow
  await page.getByTestId(`research-node-${itemLackingNodeGuid}`).click();
  await page.mouse.move(2, 2);
  await shoot(page, "small.png");
  const viewportMetrics = await page.evaluate(() => {
    const viewport = document.querySelector("[data-testid=research-detail-scroll] .mantine-ScrollArea-viewport");
    if (!viewport) throw new Error("scroll viewport missing");
    return { clientHeight: viewport.clientHeight, scrollHeight: viewport.scrollHeight };
  });
  console.log(JSON.stringify({ small: { pane: await pane.boundingBox(), viewport: viewportMetrics } }));
}

async function main() {
  process.env.MOCK_DEMO = "1";
  inflateResearchableNode();
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });
  await openResearchTree(page);
  await captureHugeAndSmallPanes(page);
  await browser.close();

  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
