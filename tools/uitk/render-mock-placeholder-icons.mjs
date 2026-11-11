// mock-host の DEMO が返すブロックの絵（placeholderIcon）を種番号ごとに PNG にする。UITK の並べ比べの撮影だけが使う
// Renders the block pictures the mock-host DEMO serves (placeholderIcon) to PNG per seed; used only by UITK comparison captures
import { mkdirSync } from "node:fs";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";
import { placeholderIcon } from "../../moorestech_web/webui/e2e/mock-host/assets/demoAssets.ts";

// tools/uitk/ には node_modules が無いので、Playwright は webui の依存から解決する（bare import はこのファイルの位置から解決されるため）
// tools/uitk/ has no node_modules, so resolve Playwright from webui's dependencies (bare imports resolve from this file's location)
const webuiRequire = createRequire(new URL("../../moorestech_web/webui/package.json", import.meta.url));
const { chromium } = webuiRequire("@playwright/test");

const [outDir, maxSeedText] = process.argv.slice(2);
if (!outDir || !maxSeedText) throw new Error("usage: render-mock-placeholder-icons.mjs <outDir> <maxSeed>");
mkdirSync(resolve(outDir), { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 64, height: 64 } });
for (let seed = 1; seed <= Number(maxSeedText); seed++) {
  // 透明の地に SVG だけを置き、そのまま撮る
  // Put only the SVG on a transparent page and capture it as is
  await page.setContent(`<html><body style="margin:0;background:transparent">${placeholderIcon(seed)}</body></html>`);
  await page.screenshot({ path: join(resolve(outDir), `block-${seed}.png`), omitBackground: true });
}
await browser.close();
