import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setTopicScenario(page, "uiVisible");
  await setTopicScenario(page, "crosshairVisible");
  await setTopicScenario(page, "miningHidden");
  await setTopicScenario(page, "tooltipHidden");
});

test("設置情報と削除警告帯を操作モードへ反映する", async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");
  const placement = page.locator('[data-tutorial-anchor~="placement.hud"]');
  await expect(placement).toContainText("Assembler");
  await expect(placement).toContainText("3");

  await setUiState(page, "DeleteBar");
  const deletion = page.getByTestId("delete-mode-warning");
  await expect(deletion).toHaveAttribute("aria-label", "Delete Mode");
  await expect(deletion.getByTestId("delete-mode-warning-band")).toHaveCount(2);
  await expect(page.locator('[data-tutorial-anchor~="delete.hud"]')).toHaveCSS("bottom", "0px");
});

test("横長画面でビネットを実viewportの四辺へ沿わせる", async ({ page }) => {
  await page.setViewportSize({ width: 2432, height: 786 });
  await setUiState(page, "GameScreen");
  await page.goto("/");

  // ビネットを実viewportで描く
  // Keep the vignette owner on the real viewport instead of the stage
  const layout = await page.locator("#root > div").evaluate((viewport) => {
    const stage = viewport.querySelector('[data-testid="app-stage"]')!;
    const rect = viewport.getBoundingClientRect();
    return {
      rect: { top: rect.top, right: rect.right, bottom: rect.bottom, left: rect.left },
      viewportBackground: getComputedStyle(viewport).backgroundImage,
      stageBackground: getComputedStyle(stage).backgroundImage,
      screen: { width: window.innerWidth, height: window.innerHeight },
    };
  });
  expect(layout.rect).toEqual({
    top: 0,
    right: layout.screen.width,
    bottom: layout.screen.height,
    left: 0,
  });
  expect(layout.viewportBackground).toContain("radial-gradient");
  expect(layout.stageBackground).toBe("none");
});

test("採掘進捗・クロスヘア・tooltipのtopic eventを表示する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "mining");
  await setTopicScenario(page, "tooltip");

  const miningProgress = page.locator('[data-tutorial-anchor~="mining.hud"] [role="progressbar"]');
  await expect(miningProgress).toHaveAttribute("aria-valuenow", "0.65");
  await expect(page.getByText(/Mining Target/i)).toHaveCount(0);
  await expect(page.getByText("Iron Ore", { exact: true })).toHaveCount(0);
  await expect(page.locator('[data-tutorial-anchor~="game.crosshair"]')).toBeVisible();
  await expect(page.getByText("世界の対象", { exact: true })).toBeVisible();

  // 書式はWeb側トークンが唯一の値源。ホスト由来の寸法へ戻らないよう実測で固定する
  // The web tokens are the only source of the format; lock the measured values so host-driven sizes cannot return
  const tooltipStyle = await page.getByTestId("cursor-tooltip").evaluate((element) => {
    const style = getComputedStyle(element);
    return { fontSize: style.fontSize, padding: style.padding, maxWidth: style.maxWidth };
  });
  expect(tooltipStyle).toEqual({ fontSize: "18px", padding: "6px 10px", maxWidth: "320px" });

  await setTopicScenario(page, "crosshairHidden");
  await expect(page.locator('[data-tutorial-anchor~="game.crosshair"]')).toBeHidden();
});

test("ツールチップは複数行を順序どおり縦積みで表示する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "tooltipMultiLine");

  const lines = page.getByTestId("cursor-tooltip-line");
  await expect(lines).toHaveCount(2);
  await expect(lines.nth(0)).toHaveText("地形に埋まっています");
  await expect(lines.nth(1)).toHaveText("遠すぎます");
});

test("ui.visibility=falseでPortalを含む全UIを退避し復帰する", async ({ page }) => {
  await setTopicScenario(page, "tooltip");
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
  await expect(page.getByText("世界の対象", { exact: true })).toBeVisible();

  await setTopicScenario(page, "uiHidden");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeHidden();
  await expect(page.getByText("世界の対象", { exact: true })).toBeHidden();

  await setTopicScenario(page, "uiVisible");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
});
