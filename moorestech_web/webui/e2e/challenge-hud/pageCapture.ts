import { join } from "node:path";
import type { Page } from "@playwright/test";
import { requestMockControl } from "../support/mockControl";
import { expectedObjectives, type CaptureCase } from "./cases";

export async function capturePage(
  page: Page,
  baseUrl: string,
  outputDirectory: string,
  captureCase: CaptureCase,
): Promise<unknown> {
  // 制御応答とDOM状態を検証してからケースを撮影する
  // Validate controls and DOM state before capturing the case
  await requestMockControl(page, baseUrl, `/__topic-control?scenario=${captureCase.scenario}`);
  if (captureCase.companionScenario !== null) {
    await requestMockControl(page, baseUrl, `/__topic-control?scenario=${captureCase.companionScenario}`);
  }
  await requestMockControl(page, baseUrl, `/__uistate?state=${captureCase.uiState}`);
  await requestMockControl(page, baseUrl, `/__skit?stage=${captureCase.skit}`);
  await page.goto(baseUrl);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await waitForExpectedState(page, captureCase);
  await applyBackground(page, captureCase.background);

  // 計測値と画像を同じ確定状態から生成する
  // Produce measurements and the image from the same settled state
  const measurement = await measure(page);
  await page.screenshot({ path: join(outputDirectory, `${captureCase.name}.png`) });
  return measurement;
}

// 本文、要素数、画面、スキットを完全一致で待機する
// Wait for exact copy, counts, screen, and skit state
async function waitForExpectedState(page: Page, captureCase: CaptureCase): Promise<void> {
  const expected = expectedObjectives[captureCase.scenario];
  const hudExpected = expected.length > 0 && captureCase.skit !== "text"
    && captureCase.uiState === "GameScreen";
  await page.waitForFunction(({ objectives, shouldShowHud, uiState, skit }) => {
    // 描画対象を一括収集し、同じフレームの可視性を比較する
    // Collect render targets together and compare visibility in one frame
    const text = Array.from(document.querySelectorAll('[data-testid="challenge-objective"]'))
      .map((element) => element.textContent ?? "");
    const hud = document.querySelector('[data-testid="challenge-hud"]');
    const inventory = document.querySelector('[data-testid="main-grid"]');
    const challengeList = document.querySelector('[data-testid="challenge-panel"]');
    const placement = document.querySelector('[data-tutorial-anchor="placement.hud"]');
    const deletion = document.querySelector('[data-tutorial-anchor="delete.hud"]');
    const backgroundSkit = document.querySelector('[data-testid="background-skit"]');
    const blockingSkit = document.querySelector('[data-testid="blocking-skit"]');
    const visibleStates = [hud, inventory, challengeList, placement, deletion, backgroundSkit, blockingSkit]
      .map((element) => {
        const style = element ? getComputedStyle(element) : null;
        const rect = element?.getBoundingClientRect();
        return Boolean(style && rect && style.display !== "none" && style.visibility === "visible"
          && Number.parseFloat(style.opacity) > 0 && rect.width > 0 && rect.height > 0);
      });
    const [hudVisible, inventoryVisible, challengeListVisible, placementVisible,
      deletionVisible, backgroundSkitVisible, blockingSkitVisible] = visibleStates;

    // HUD本文と隣接UIの双方をケース定義へ完全一致させる
    // Match both HUD copy and neighboring UI exactly to the case definition
    const hudMatches = shouldShowHud
      ? hudVisible && hud?.getAttribute("aria-label") === "現在のチャレンジ"
        && JSON.stringify(text) === JSON.stringify(objectives)
      : !hudVisible && text.length === 0;
    const panelsMatch = inventoryVisible === (uiState === "PlayerInventory")
      && challengeListVisible === (uiState === "ChallengeList")
      && placementVisible === (uiState === "PlaceBlock")
      && deletionVisible === (uiState === "DeleteBar");
    const skitMatches = backgroundSkitVisible === (skit === "background")
      && blockingSkitVisible === (skit === "text");
    return hudMatches && panelsMatch && skitMatches;
  }, { objectives: [...expected], shouldShowHud: hudExpected, uiState: captureCase.uiState, skit: captureCase.skit });
}

// 明暗単色で世界画像とは独立したコントラスト限界を露出させる
// Expose contrast limits on bright and dark solids independently of the world image
async function applyBackground(page: Page, background: CaptureCase["background"]): Promise<void> {
  if (background === "world") return;
  const color = background === "bright" ? "rgb(225, 229, 235)" : "rgb(12, 15, 22)";
  const world = page.locator("#__worldbg");
  await world.evaluate((element, value) => {
    (element as HTMLElement).style.background = value;
  }, color);
  await page.waitForFunction((value) =>
    getComputedStyle(document.querySelector<HTMLElement>("#__worldbg")!).backgroundColor === value, color);
}

async function measure(page: Page): Promise<unknown> {
  // 各画像にHUD寸法、入力透過、折返し計測を対応付ける
  // Pair each image with HUD geometry, input pass-through, and wrapping metrics
  return page.evaluate(() => {
    const hud = document.querySelector<HTMLElement>('[data-testid="challenge-hud"]');
    const hudRect = hud?.getBoundingClientRect();
    const objectives = Array.from(document.querySelectorAll<HTMLElement>('[data-testid="challenge-objective"]'));
    const operationHud = document.querySelector<HTMLElement>(
      '[data-testid="placement-mode-hud"], [data-testid="delete-mode-hud"]',
    );
    const operationHudRect = operationHud?.getBoundingClientRect();
    const operationHudStyle = operationHud ? getComputedStyle(operationHud) : null;
    const warning = operationHud?.querySelector<HTMLElement>('[data-testid="operation-mode-warning"]');
    return {
      hud: hudRect ? { x: hudRect.x, y: hudRect.y, width: hudRect.width, height: hudRect.height } : null,
      label: hud?.getAttribute("aria-label") ?? null,
      background: hud ? getComputedStyle(hud).backgroundColor : null,
      pointerEvents: hud ? getComputedStyle(hud).pointerEvents : null,
      objectives: objectives.map((objective) => {
        const rect = objective.getBoundingClientRect();
        return {
          text: objective.textContent, right: rect.right, clientWidth: objective.clientWidth,
          scrollWidth: objective.scrollWidth, clientHeight: objective.clientHeight,
          lineHeight: Number.parseFloat(getComputedStyle(objective).lineHeight),
        };
      }),
      operationHud: operationHud && operationHudRect && operationHudStyle ? {
        kind: operationHud.dataset.testid,
        x: operationHudRect.x,
        y: operationHudRect.y,
        width: operationHudRect.width,
        height: operationHudRect.height,
        background: operationHudStyle.backgroundColor,
        borderWidth: operationHudStyle.borderWidth,
        boxShadow: operationHudStyle.boxShadow,
        pointerEvents: operationHudStyle.pointerEvents,
        warningColor: warning ? getComputedStyle(warning).color : null,
      } : null,
    };
  });
}
