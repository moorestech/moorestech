import { test, expect } from "@playwright/test";
import { setSkitStage, setTopicScenario, setUiState } from "../support/mockControl";
import { expectAbove, expectSeparatedHorizontally, expectWithinViewport } from "../support/layoutAssertions";
import { expectChallengeHudPresentation, readChallengeHudPresentation } from "../support/challengeHudAssertions";

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
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
});

test("常駐HUDをインベントリ・メニュー・操作モードで維持する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  const initialWorldPresentation = await readChallengeHudPresentation(page);
  await expect(page.getByTestId("challenge-panel")).toHaveCount(0);
  // インベントリでも常駐表示を維持する
  // Retain the resident display in the inventory
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("main-grid")).toBeVisible();
  await expectChallengeHudPresentation(page, initialWorldPresentation);

  // 操作モードと常駐HUDを分離する
  // Separate operation cues from the resident HUD
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  const placementHud = page.locator('[data-tutorial-anchor="placement.hud"]');
  const challengeHud = page.getByTestId("challenge-hud");
  await expect(placementHud).toBeVisible();
  await expectChallengeHudPresentation(page, initialWorldPresentation);
  await expectSeparatedHorizontally(challengeHud, placementHud);
  await setUiState(page, "DeleteBar");
  const deleteWarning = page.getByTestId("delete-mode-warning");
  await expect(deleteWarning).toBeVisible();
  await expectChallengeHudPresentation(page, initialWorldPresentation);
  const topBand = deleteWarning.getByTestId("delete-mode-warning-band").first();
  await expectAbove(topBand, challengeHud);
  await setUiState(page, "ChallengeList");
  await expect(page.getByTestId("challenge-panel")).toBeVisible();
  await expectChallengeHudPresentation(page, initialWorldPresentation);

  // 全メニューで常駐表示を維持する
  // Retain the resident display in every menu
  await setTopicScenario(page, "challengeMultipleLong");
  await setUiState(page, "GameScreen");
  const worldPresentation = await readChallengeHudPresentation(page);
  // 研究パネルのみ持ち物の右を上端まで占有するため上部安全帯を持たない（ADR 0014）
  // The research panel alone occupies the area right of the inventory up to the top edge, so it has no upper safe area (ADR 0014)
  const upperSafeMenus = [
    ["PlayerInventory", undefined, "main-grid", true],
    ["SubInventory", undefined, "main-grid", true],
    ["ResearchTree", undefined, "research-tree", false],
    ["BuildMenu", undefined, "build-menu-panel", true],
    ["ChallengeList", undefined, "challenge-panel", true],
    ["PauseMenu", undefined, "pause-menu", true],
    ["TrainHUDScreen", "PauseMenuScreen", "pause-menu", true],
  ] as const;
  for (const [state, subState, contentTestId, hasUpperSafeArea] of upperSafeMenus) {
    await setUiState(page, state, subState);
    const menuContent = page.getByTestId(contentTestId);
    await expect(menuContent).toBeVisible();
    await expectChallengeHudPresentation(page, worldPresentation);
    if (hasUpperSafeArea) await expectAbove(challengeHud, menuContent);
  }

  // 左配置のHUDを一覧より上へ分離する
  // Keep the left-aligned HUD above the fullscreen challenge controls
  await setUiState(page, "ChallengeList");
  await expectChallengeHudPresentation(page, worldPresentation);
  await expectAbove(challengeHud, page.getByTestId("challenge-category-81000000-0000-4000-8000-000000000001"));

  // 縮小画面でもHUDを画面内へ収める
  // Follow stage scaling and remain within a smaller viewport
  await page.setViewportSize({ width: 1024, height: 576 });
  await setUiState(page, "GameScreen");
  await expect.poll(async () => (await challengeHud.boundingBox())?.width).toBeCloseTo(560 * 1024 / 1280, 1);
  const scaledWorldPresentation = await readChallengeHudPresentation(page);
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("main-grid")).toBeVisible();
  await expectChallengeHudPresentation(page, scaledWorldPresentation);
  await expectWithinViewport(challengeHud);
  await expectAbove(challengeHud, page.getByTestId("main-grid"));
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

test("背景スキット中は進行中チャレンジの描画契約を維持する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await setSkitStage(page, "none");
  await page.goto("/");
  const worldPresentation = await readChallengeHudPresentation(page);
  await setSkitStage(page, "background");
  await expect(page.getByTestId("background-skit")).toBeVisible();
  await expectChallengeHudPresentation(page, worldPresentation);
});
