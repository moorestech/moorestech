import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";

// Web UIの依存を計測スクリプト位置ではなくpackage起点で解決する
// Resolve Web UI dependencies from its package rather than this measurement directory
const webUiRequire = createRequire(new URL("../../../../moorestech_web/webui/package.json", import.meta.url));
const { chromium } = webUiRequire("@playwright/test");
const { WebSocketServer } = webUiRequire("ws");

const sizes = ["8.70", "8.71", "8.72", "8.74", "8.75", "8.77", "8.79", "8.80", "8.82", "8.83", "8.85", "8.86", "8.88", "8.91", "8.93", "8.94", "8.96", "8.97", "8.99", "9.00", "9.02", "9.04", "9.05", "9.07", "9.11", "9.13", "9.15", "9.16", "9.18"];
const insets = ["7.00", "6.99", "6.98", "6.97", "6.96", "6.94", "6.93", "6.91", "6.87", "6.86", "6.85", "6.83", "6.82", "6.81", "6.69", "6.68", "6.62", "6.50"];
const manifest = process.argv[2];

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("../../../../moorestech_web/webui/e2e/mock-host/httpHandler");
  const { attachWsHandlers } = await import("../../../../moorestech_web/webui/e2e/mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(5417, resolve));
  const browser = await chromium.launch();
  const page = await (await browser.newContext({ viewport: { width: 1635, height: 922 }, deviceScaleFactor: 2 })).newPage();
  // 正本条件で描画を安定待機する
  // Use reference state and settle rendering
  await page.goto("http://127.0.0.1:5417/");
  await page.getByRole("heading", { name: "CRAFT RECIPE" }).waitFor();
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await page.locator('[class*="_recipeBox_"]').waitFor();
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  const rows = ["size_token\tinset_token\tcomputed_width\tcomputed_height\tright\tbottom\tpanel_rect\tpseudo_rect\tcapture_file\tsha256"];
  // 描画後のDOM値と画像ハッシュを同じ行へ固定する
  // Lock post-render DOM values and the image hash into the same row
  for (const size of sizes) {
    for (const inset of insets) {
      // 各組み合わせのtokenを先に反映して描画完了を待つ
      // Apply each pair's tokens first, then wait for rendering to settle
      await page.evaluate((values) => {
        document.documentElement.style.setProperty("--craft-grip-size", `${values[0]}px`);
        document.documentElement.style.setProperty("--craft-grip-inset", `${values[1]}px`);
      }, [size, inset]);
      await page.waitForTimeout(400);
      // 待機後に実DOM値を読み取り、その直後の画面を撮影する
      // Read live DOM values after the wait and capture that same rendered state
      const dom = await page.locator('[data-variant="craft"]').evaluateAll((elements) => {
        const frame = elements.map((element) => ({ element, box: element.getBoundingClientRect() })).sort((a, b) => b.box.width * b.box.height - a.box.width * a.box.height)[0].element;
        const panel = frame.getBoundingClientRect();
        const style = getComputedStyle(frame, "::after");
        const width = Number.parseFloat(style.width);
        const height = Number.parseFloat(style.height);
        const right = Number.parseFloat(style.right);
        const bottom = Number.parseFloat(style.bottom);
        return [style.width, style.height, style.right, style.bottom, JSON.stringify([panel.left, panel.top, panel.right, panel.bottom]), JSON.stringify([panel.right - right - width, panel.bottom - bottom - height, panel.right - right, panel.bottom - bottom])];
      });
      const capture = `/tmp/webui-craft-round4-s${size}-i${inset}.png`;
      await page.screenshot({ path: capture });
      const digest = createHash("sha256").update(readFileSync(capture)).digest("hex");
      rows.push([size, inset, ...dom, capture, digest].join("\t"));
    }
  }
  writeFileSync(manifest, `${rows.join("\n")}\n`);
  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
