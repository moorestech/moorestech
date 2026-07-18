import { expect, test } from "@playwright/test";
import { setBlock, setTrainRiding, setUiState } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTrainRiding(page, false, 0, 0);
  await setBlock(page, "closed");
  await setUiState(page, "PlayerInventory");
});

test("乗車HUDと分岐選択を表示し、入れ子Pauseへ遷移する", async ({ page }) => {
  await setTrainRiding(page, true, 3, 1);
  await setUiState(page, "TrainHUDScreen", "GameScreen");
  await page.goto("/");

  await expect(page.getByTestId("train-riding-hud")).toBeVisible();
  await expect(page.getByTestId("train-branch-selection")).toContainText("2/3");

  await setUiState(page, "TrainHUDScreen", "PauseMenuScreen");
  await expect(page.getByTestId("pause-menu")).toBeVisible();
});

test("貨車スロットとコンテナ不在エラーを表示する", async ({ page }) => {
  await setBlock(page, "train");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("train-inventory-slots")).toBeVisible();

  await setBlock(page, "trainError");
  await expect(page.getByTestId("train-inventory-error")).toContainText("This train has no item container");
});
