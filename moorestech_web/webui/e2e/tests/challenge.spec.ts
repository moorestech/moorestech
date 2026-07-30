import { test, expect } from "@playwright/test";
import { setSkitStage, setTopicScenario, setUiState } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await setSkitStage(page, "none");
  await setUiState(page, "PlayerInventory");
  await setTopicScenario(page, "japanese");
});

test("challenge.current完了eventで進行HUDを更新する", async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toContainText("Second Step");

  await setTopicScenario(page, "challengeCompleted");
  await expect(page.getByTestId("challenge-hud")).toBeHidden();
});

// 専用画面と内部キー非表示を検証する
// Verify the dedicated screen and absence of internal keys
test("チャレンジ画面が開きツリーだけを翻訳済み表示する", async ({ page }) => {
  await setUiState(page, "ChallengeList");
  await page.goto("/");
  await expect(page.getByTestId("challenge-panel")).toBeVisible();
  await expect(page.getByTestId("challenge-category-81000000-0000-4000-8000-000000000001")).toHaveText("Basics");
  await expect(page.getByTestId("challenge-node-82000000-0000-4000-8000-000000000001")).toBeVisible();
  await expect(page.getByTestId("challenge-node-82000000-0000-4000-8000-000000000002")).toBeVisible();
  await expect(page.getByRole("heading", { name: "チャレンジ" })).toBeVisible();
  await expect(page.getByText("First Craft")).toBeVisible();
  await expect(page.getByText("完了", { exact: true })).toBeVisible();
  await expect(page.getByText("進行中", { exact: true })).toBeVisible();
  await expect(page.locator("body")).not.toContainText("challenge.");
  await expect(page.getByTestId("challenge-hud")).toHaveCount(0);
});

test("常駐HUDをモーダル画面と操作モードだけで隠す", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  await expect(page.getByTestId("challenge-panel")).toHaveCount(0);

  // モーダルの情報集約を検証する
  // Verify modal information consolidation
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("main-grid")).toBeVisible();
  await expect(page.getByTestId("challenge-hud")).toHaveCount(0);

  // 操作HUDの単独表示を検証する
  // Verify exclusive operation-HUD display
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await expect(page.locator('[data-tutorial-anchor="placement.hud"]')).toBeVisible();
  await expect(page.getByTestId("challenge-hud")).toHaveCount(0);

  await setTopicScenario(page, "delete");
  await setUiState(page, "DeleteBar");
  await expect(page.locator('[data-tutorial-anchor="delete.hud"]')).toBeVisible();
  await expect(page.getByTestId("challenge-hud")).toHaveCount(0);

  // 非モーダル時の常駐HUDを検証する
  // Verify the resident HUD in non-modal states
  await setUiState(page, "ChallengeList");
  await expect(page.getByTestId("challenge-panel")).toBeVisible();
  await expect(page.getByTestId("challenge-hud")).toHaveCount(0);

  await setUiState(page, "TrainHUDScreen", "GameScreen");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  await setUiState(page, "Debug");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("challenge-panel")).toHaveCount(0);
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
});

test("言語辞書世代の更新だけで進行中チャレンジを再解決する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setTopicScenario(page, "japanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const objective = page.getByTestId("challenge-objective");
  await expect(objective).toHaveText("石を採掘する");

  await setTopicScenario(page, "english");
  await expect(objective).toHaveText("Mine stone");
});

test("blockingスキット中だけ進行中チャレンジを隠す", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await setSkitStage(page, "none");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  await setSkitStage(page, "text");
  await expect(page.getByTestId("challenge-hud")).toBeHidden();
  await setSkitStage(page, "none");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
});
