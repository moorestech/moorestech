// Task 7 目視QA: 機械UI改修（ADR 0010）の矢印グリフ・中心線・フッタ・タブ順を撮影して実測する
// Task 7 visual QA: capture and measure the machine UI refresh (ADR 0010) arrow glyph, center line, footer, and tab order

import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5411);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/machine-qa";
const VIEWPORT = { width: 1280, height: 720 } as const;
const CROP_PADDING_PX = 40;

async function shotWithBox(page: Page, name: string, box: { x: number; y: number; width: number; height: number } | null, padding = CROP_PADDING_PX) {
  if (box === null) throw new Error(`${name}: bounding box missing`);
  const x = Math.max(0, box.x - padding);
  const y = Math.max(0, box.y - padding);
  const width = Math.min(VIEWPORT.width - x, box.width + padding * 2);
  const height = Math.min(VIEWPORT.height - y, box.height + padding * 2);
  const path = join(OUT_DIR, name);
  await page.screenshot({ path, clip: { x, y, width, height } });
  return path;
}

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  attachWsHandlers(wss);
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 2 });
  const manifest: Record<string, unknown> = {};

  async function captureCraft() {
    const page = await context.newPage();
    await page.goto(`http://127.0.0.1:${PORT}/`);
    await page.getByRole("heading", { name: "CRAFT RECIPE" }).waitFor();
    await page.getByTestId("item-list-grid").locator("> div").first().click();
    await page.locator('[class*="_recipeBox_"]').waitFor();
    await page.evaluate("document.fonts.ready.then(() => undefined)");
    await page.mouse.move(2, 2);
    await page.waitForTimeout(300);
    const arrow = page.getByTestId("craft-progress-arrow");
    const arrowBox = await arrow.boundingBox();
    await page.screenshot({ path: join(OUT_DIR, "craft-full.png") });
    await shotWithBox(page, "craft-arrow-crop.png", arrowBox, 24);
    manifest.craftArrowBox = arrowBox;
    await page.close();
  }

  async function captureMachine() {
    const page = await context.newPage();
    await page.request.get(`http://127.0.0.1:${PORT}/__block?type=machine`);
    await page.goto(`http://127.0.0.1:${PORT}/`);
    await page.getByTestId("block-inventory").waitFor();
    await page.evaluate("document.fonts.ready.then(() => undefined)");
    await page.mouse.move(2, 2);
    await page.waitForTimeout(300);

    // タブ順・初期タブの実測（レシピ選択が先頭・選択済みなのでインベントリが初期表示）
    // Measure tab order and initial tab (recipes first; selected recipe defaults to the inventory tab)
    const tabButtons = page.getByTestId("machine-tab-switch").locator("button");
    manifest.machineTabOrder = await tabButtons.evaluateAll((els) => els.map((el) => el.getAttribute("data-testid")));
    manifest.machineInitialTabPressed = await page.getByTestId("machine-tab-inventory").getAttribute("aria-pressed");

    const panel = page.getByTestId("block-inventory");
    const arrow = page.getByTestId("machine-progress-arrow");
    const processRow = page.locator('[class*="_processRow_"]');
    const inputSlots = page.getByTestId("machine-input-slots");
    const outputSlots = page.getByTestId("machine-output-slots");
    const stateLabel = page.getByTestId("machine-state-label");
    const powerRate = page.getByTestId("machine-power-rate");

    const [panelBox, arrowBox, processRowBox, inputBox, outputBox, stateLabelBox, powerRateBox] = await Promise.all([
      panel.boundingBox(),
      arrow.boundingBox(),
      processRow.boundingBox(),
      inputSlots.boundingBox(),
      outputSlots.boundingBox(),
      stateLabel.boundingBox(),
      powerRate.boundingBox(),
    ]);

    await page.screenshot({ path: join(OUT_DIR, "machine-full.png") });
    await shotWithBox(page, "machine-arrow-crop.png", arrowBox, 24);
    await shotWithBox(page, "machine-processrow-crop.png", processRowBox, 16);
    if (panelBox && stateLabelBox && powerRateBox) {
      const footerTop = Math.min(stateLabelBox.y, powerRateBox.y) - 16;
      await page.screenshot({
        path: join(OUT_DIR, "machine-footer-crop.png"),
        clip: { x: panelBox.x, y: footerTop, width: panelBox.width, height: panelBox.y + panelBox.height - footerTop },
      });
    }

    manifest.machineArrowBox = arrowBox;
    manifest.machineProcessRowBox = processRowBox;
    manifest.machinePanelBox = panelBox;
    manifest.machineInputBox = inputBox;
    manifest.machineOutputBox = outputBox;
    manifest.machineStateLabelText = await stateLabel.textContent();
    manifest.machinePowerRateText = await powerRate.textContent();
    if (arrowBox && processRowBox) {
      manifest.machineArrowCenterX = arrowBox.x + arrowBox.width / 2;
      manifest.processRowCenterX = processRowBox.x + processRowBox.width / 2;
      manifest.centerDeltaPx = Math.abs((arrowBox.x + arrowBox.width / 2) - (processRowBox.x + processRowBox.width / 2));
    }
    if (panelBox && stateLabelBox) {
      manifest.footerBottomMarginPx = panelBox.y + panelBox.height - (stateLabelBox.y + stateLabelBox.height);
    }
    await page.close();
  }

  async function captureGearMachine() {
    const page = await context.newPage();
    await page.request.get(`http://127.0.0.1:${PORT}/__block?type=gearMachine`);
    await page.goto(`http://127.0.0.1:${PORT}/`);
    await page.getByTestId("block-inventory").waitFor();
    await page.evaluate("document.fonts.ready.then(() => undefined)");
    await page.mouse.move(2, 2);
    await page.waitForTimeout(300);

    const tabButtons = page.getByTestId("machine-tab-switch").locator("button");
    manifest.gearMachineTabOrder = await tabButtons.evaluateAll((els) => els.map((el) => el.getAttribute("data-testid")));
    manifest.gearMachineInitialTabPressed = await page.getByTestId("machine-tab-recipes").getAttribute("aria-pressed");
    manifest.gearMachineRecipeSelectionVisible = await page.getByTestId("machine-recipe-selection").isVisible();

    const stateLabel = page.getByTestId("machine-state-label");
    const powerRate = page.getByTestId("machine-power-rate");
    manifest.gearMachineStateLabelText = await stateLabel.textContent();
    manifest.gearMachinePowerRateText = await powerRate.textContent();
    const panel = page.getByTestId("block-inventory");
    const [panelBox, stateLabelBox, powerRateBox] = await Promise.all([
      panel.boundingBox(),
      stateLabel.boundingBox(),
      powerRate.boundingBox(),
    ]);

    await page.screenshot({ path: join(OUT_DIR, "gearmachine-full.png") });
    if (panelBox && stateLabelBox && powerRateBox) {
      const footerTop = Math.min(stateLabelBox.y, powerRateBox.y) - 16;
      await page.screenshot({
        path: join(OUT_DIR, "gearmachine-footer-crop.png"),
        clip: { x: panelBox.x, y: footerTop, width: panelBox.width, height: panelBox.y + panelBox.height - footerTop },
      });
    }
    await page.close();
  }

  await captureCraft();
  await captureMachine();
  await captureGearMachine();

  const files = await Promise.all(
    ["craft-full.png", "craft-arrow-crop.png", "machine-full.png", "machine-arrow-crop.png", "machine-processrow-crop.png", "machine-footer-crop.png", "gearmachine-full.png", "gearmachine-footer-crop.png"]
      .map(async (name) => {
        try {
          const buf = await readFile(join(OUT_DIR, name));
          return { name, sha256: createHash("sha256").update(buf).digest("hex") };
        } catch {
          return { name, sha256: null };
        }
      }),
  );
  await writeFile(join(OUT_DIR, "manifest.json"), `${JSON.stringify({ generatedAt: new Date().toISOString(), viewport: VIEWPORT, ...manifest, captures: files }, null, 2)}\n`);

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
