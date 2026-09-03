// 縁取りの目視QA撮影（ADR 0033）
// Visual QA capture for the outline (ADR 0033)

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";
import { WebSocketServer } from "ws";
import { itemLackingNodeGuid } from "./mock-host/researchFixtures";

const PORT = Number(process.env.CAPTURE_PORT ?? 5391);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/icon-text-outline";
const VIEWPORT = { width: 1280, height: 720 } as const;

async function open(page: Page, state: string, block: string) {
  await page.request.get(`http://127.0.0.1:${PORT}/__block?type=${block}`);
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=${state}`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.mouse.move(2, 2);
}

// 縁がCSSで効いていることを該当要素すべてについて数値で押さえる
// Measure the resolved stroke on every matching element, not just the first
async function measureAll(page: Page, selector: string) {
  return page.evaluate((sel) => [...document.querySelectorAll(sel)].map((el) => {
    const cs = getComputedStyle(el);
    return {
      text: el.textContent,
      fontSize: cs.fontSize,
      color: cs.color,
      strokeWidth: cs.webkitTextStrokeWidth,
      strokeColor: cs.webkitTextStrokeColor,
      paintOrder: cs.paintOrder,
      textShadow: cs.textShadow,
    };
  }), selector);
}

type Screen = {
  name: string;
  selector: string;
  // 画面ごとに必ず写るべき系統をfont-sizeで宣言し、空振りの合格を潰す
  // Each screen declares the font sizes that must appear, so an empty capture cannot pass
  requiredFontSizes: string[];
  show: (page: Page) => Promise<void>;
};

const screens: Screen[] = [
  {
    name: "inventory",
    selector: ".iconTextOutlineLight",
    requiredFontSizes: [],
    show: async (page) => {
      await open(page, "PlayerInventory", "closed");
      await page.getByTestId("item-list-grid").waitFor();
    },
  },
  {
    name: "recipe",
    selector: ".iconTextOutlineLight",
    requiredFontSizes: ["11px"],
    show: async (page) => {
      await open(page, "PlayerInventory", "closed");
      await page.getByTestId("item-list-grid").locator("> div").first().click();
      await page.locator('[data-testid^="craft-recipe-entry"]').first().waitFor();
    },
  },
  {
    name: "research",
    selector: ".iconTextOutlineLight",
    requiredFontSizes: ["11px"],
    show: async (page) => {
      await page.request.get(`http://127.0.0.1:${PORT}/__research`);
      await page.request.get(`http://127.0.0.1:${PORT}/__topic-control?scenario=researchOwnedItems`);
      await open(page, "ResearchTree", "closed");
      await page.getByTestId(`research-node-${itemLackingNodeGuid}`).click();
      await page.getByTestId("research-consume-items").waitFor();
      await page.mouse.move(2, 2);
    },
  },
  {
    name: "fluid",
    selector: ".iconTextOutlineDark",
    requiredFontSizes: ["12px"],
    show: async (page) => {
      await open(page, "SubInventory", "tank");
      await page.locator(".iconTextOutlineDark").first().waitFor();
    },
  },
  {
    name: "hotbar",
    selector: ".iconTextOutlineDark",
    requiredFontSizes: ["13px"],
    show: async (page) => {
      await open(page, "GameScreen", "closed");
      await page.getByTestId("hotbar-grid").waitFor();
    },
  },
];

async function main() {
  // 背景付きmock hostを専用ポートで起動（capture-mining-progress.ts踏襲）
  // Boot the background mock host on its capture port (follows capture-mining-progress.ts)
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);

  await mkdir(OUT_DIR, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });

  for (const screen of screens) {
    await screen.show(page);
    const measured = await measureAll(page, screen.selector);
    console.log(screen.name, JSON.stringify(measured));
    // 1件も無い／必要な系統が欠けた撮影は失敗として落とす
    // A capture with no match, or missing a required family, fails the run
    if (measured.length === 0) throw new Error(`${screen.name}: ${screen.selector} matched nothing`);
    const sizes = new Set(measured.map((measurement) => measurement.fontSize));
    const missing = screen.requiredFontSizes.filter((size) => !sizes.has(size));
    if (missing.length > 0) throw new Error(`${screen.name}: missing font sizes ${missing.join(",")} (saw ${[...sizes].join(",")})`);
    await page.screenshot({ path: join(OUT_DIR, `${screen.name}.png`) });
  }

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
