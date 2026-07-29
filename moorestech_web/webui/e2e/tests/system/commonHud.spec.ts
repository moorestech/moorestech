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
  const placement = page.locator('[data-tutorial-anchor="placement.hud"]');
  await expect(placement).toContainText("Assembler");
  await expect(placement).toContainText("3");

  await setTopicScenario(page, "delete");
  await setUiState(page, "DeleteBar");
  const deletion = page.getByTestId("delete-mode-warning");
  await expect(deletion).toHaveAttribute("aria-label", "Delete Mode");
  await expect(deletion.getByTestId("delete-mode-warning-band")).toHaveCount(2);
  await expect(page.locator('[data-tutorial-anchor="delete.hud"]')).toHaveCSS("bottom", "0px");
  await expect(page.getByText("Protected area", { exact: true })).toHaveCount(0);
});

test("採掘進捗・クロスヘア・tooltipのtopic eventを表示する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "mining");
  await setTopicScenario(page, "tooltip");

  const miningProgress = page.locator('[data-tutorial-anchor="mining.hud"] [role="progressbar"]');
  await expect(miningProgress).toHaveAttribute("aria-valuenow", "0.65");
  await expect(page.getByText(/Mining Target/i)).toHaveCount(0);
  await expect(page.getByText("Iron Ore", { exact: true })).toHaveCount(0);
  await expect(page.locator('[data-tutorial-anchor="game.crosshair"]')).toBeVisible();
  await expect(page.getByText("世界の対象", { exact: true })).toBeVisible();

  await setTopicScenario(page, "crosshairHidden");
  await expect(page.locator('[data-tutorial-anchor="game.crosshair"]')).toBeHidden();
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
