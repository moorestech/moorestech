// 縁取りの目視QA撮影（ADR 0033）
// Visual QA capture for the outline (ADR 0033)

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";

const PORT = Number(process.env.CAPTURE_PORT ?? 5391);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/icon-text-outline";
const VIEWPORT = { width: 1280, height: 720 } as const;

async function open(page: Page, state: string, block: string) {
  await page.request.get(`http://127.0.0.1:${PORT}/__block?type=${block}`);
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=${state}`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.mouse.move(2, 2);
  await page.waitForTimeout(500);
}

// 縁がCSSで効いていることを数値で押さえる
// Measure that the stroke resolved in CSS
async function measure(page: Page, selector: string) {
  return page.evaluate((sel) => {
    const el = document.querySelector(sel);
    if (!el) return { selector: sel, found: false };
    const cs = getComputedStyle(el);
    return {
      selector: sel,
      found: true,
      text: el.textContent,
      fontSize: cs.fontSize,
      color: cs.color,
      strokeWidth: cs.webkitTextStrokeWidth,
      strokeColor: cs.webkitTextStrokeColor,
      paintOrder: cs.paintOrder,
      textShadow: cs.textShadow,
    };
  }, selector);
}

async function main() {
  await mkdir(OUT_DIR, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });

  const screens = [
    { name: "inventory", state: "PlayerInventory", block: "closed", selectors: [".iconTextOutlineLight"] },
    { name: "research", state: "ResearchTree", block: "closed", selectors: [".iconTextOutlineLight"] },
    { name: "fluid", state: "SubInventory", block: "tank", selectors: [".iconTextOutlineDark"] },
    { name: "hotbar", state: "GameScreen", block: "closed", selectors: [".iconTextOutlineDark"] },
  ];

  for (const screen of screens) {
    await open(page, screen.state, screen.block);
    for (const selector of screen.selectors) {
      console.log(screen.name, JSON.stringify(await measure(page, selector)));
    }
    await page.screenshot({ path: join(OUT_DIR, `${screen.name}.png`) });
  }

  await browser.close();
}

void main();
