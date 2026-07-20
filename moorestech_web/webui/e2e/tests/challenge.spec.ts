import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await setUiState(page, "PlayerInventory");
});

test("challenge.current完了eventで進行HUDを更新する", async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toContainText("Second Step");

  await setTopicScenario(page, "challengeCompleted");
  await expect(page.getByTestId("challenge-hud")).toBeHidden();
});

// ChallengeListルーティングでツリーが表示され、常駐HUDが進行中チャレンジを示す
// The ChallengeList route renders the tree and the resident HUD shows the active challenge
test("チャレンジ画面が開きツリーとHUDが表示される", async ({ page }) => {
  await setUiState(page, "ChallengeList");
  await page.goto("/");
  await expect(page.getByTestId("challenge-panel")).toBeVisible();
  await expect(page.getByText("First Craft")).toBeVisible();
  await expect(page.getByTestId("challenge-hud")).toContainText("Second Step");
});
