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
  await expect(page.getByRole("heading", { name: "Pause Menu" })).toBeVisible();
  await expect(page.getByRole("button", { name: "ゲームをセーブする" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-locale", "japanese");

  await setTopicScenario(page, "english");
  await expect(page.getByRole("heading", { name: "Pause Menu" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save this game" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-locale", "english");
});

test("ポーズメニューの言語選択で英語と日本語を往復する", async ({ page }) => {
  await setTopicScenario(page, "japanese");
  await setUiState(page, "PauseMenu");
  await page.goto("/");

  const languageSection = page.getByRole("region", { name: "言語" });
  const english = page.getByRole("button", { name: "English" });
  const japanese = page.getByRole("button", { name: "日本語" });
  await expect(languageSection).toContainText("English");
  await expect(japanese).toHaveAttribute("aria-pressed", "true");
  await expect(english).toHaveAttribute("aria-pressed", "false");

  await english.click();
  await expect(page.getByRole("heading", { name: "Language" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save this game" })).toBeVisible();
  await expect(english).toHaveAttribute("aria-pressed", "true");

  await japanese.click();
  await expect(page.getByRole("heading", { name: "言語" })).toBeVisible();
  await expect(page.getByRole("button", { name: "ゲームをセーブする" })).toBeVisible();
  await expect(japanese).toHaveAttribute("aria-pressed", "true");
});
