import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "japanese");
  await setUiState(page, "PlayerInventory");
});

test("localization.current切替でlocale別辞書を再取得し表示を更新する", async ({ page }) => {
  await setTopicScenario(page, "japanese");
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "ポーズメニュー" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-locale", "japanese");

  await setTopicScenario(page, "english");
  await expect(page.getByRole("heading", { name: "Pause Menu" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save Game" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-locale", "english");
});
