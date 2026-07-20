import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setTopicScenario(page, "uiVisible");
  await setTopicScenario(page, "crosshairVisible");
  await setTopicScenario(page, "keyHintsHidden");
  await setTopicScenario(page, "miningHidden");
  await setTopicScenario(page, "tooltipHidden");
});

test("設置・削除モードtopicをHUDへ反映する", async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");
  const placement = page.locator('[data-tutorial-anchor="placement.hud"]');
  await expect(placement).toContainText("Assembler");
  await expect(placement).toContainText("3");
  await expect(placement).toContainText("Energized Range");

  await setTopicScenario(page, "delete");
  await setUiState(page, "DeleteBar");
  const deletion = page.locator('[data-tutorial-anchor="delete.hud"]');
  await expect(deletion).toContainText("Delete Mode");
  await expect(deletion).toContainText("Protected area");
});

test("採掘・キーヒント・クロスヘア・tooltipのtopic eventを表示する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "mining");
  await setTopicScenario(page, "keyHints");
  await setTopicScenario(page, "tooltip");

  await expect(page.locator('[data-tutorial-anchor="mining.hud"]')).toContainText("Iron Ore");
  await expect(page.locator('[data-tutorial-anchor="mining.hud"] [role="progressbar"]')).toHaveAttribute("aria-valuenow", "65");
  await expect(page.locator('[data-tutorial-anchor="game.key-hints"]')).toContainText("操作ヒント");
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
