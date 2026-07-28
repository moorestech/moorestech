import { test, expect } from "@playwright/test";
import { setSkitStage, setTopicScenario, setUiState } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await setSkitStage(page, "none");
  await setUiState(page, "PlayerInventory");
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
  await expect(page.getByTestId("challenge-category-cat-1")).toHaveText("Basics");
  await expect(page.getByTestId("challenge-node-ch-1")).toBeVisible();
  await expect(page.getByTestId("challenge-node-ch-2")).toBeVisible();
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

test("進行中チャレンジを内部キーやカード面なしで表示する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const hud = page.getByTestId("challenge-hud");
  await expect(hud).toContainText("現在のチャレンジ");
  await expect(hud).toContainText("石を採掘する");
  await expect(hud).not.toContainText("challenge.current");
  await expect(hud).toHaveCSS("pointer-events", "none");
  await expect(hud).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");

  // 固定配置と影をピクセル検証する
  // Verify fixed placement and shadow in pixels
  await expect(hud).toHaveCSS("top", "24px");
  await expect(hud).toHaveCSS("left", "24px");
  await expect(hud).toHaveCSS("width", "288px");
  await expect(hud).toHaveCSS("text-shadow", "rgba(0, 0, 0, 0.85) 0px 1px 2px");
  await expect(hud.locator('[aria-hidden="true"]')).toHaveCount(1);

  // 面装飾と文字階層をスタイル検証する
  // Verify surface decoration and type hierarchy through styles
  const visualContract = await hud.evaluate((element) => {
    const hudStyle = getComputedStyle(element);
    const labelStyle = getComputedStyle(element.firstElementChild!);
    const objectiveStyle = getComputedStyle(element.querySelector('[data-testid="challenge-objective"]')!);
    return {
      animationName: hudStyle.animationName,
      borderRadius: hudStyle.borderRadius,
      borderWidth: hudStyle.borderWidth,
      boxShadow: hudStyle.boxShadow,
      fontWeight: objectiveStyle.fontWeight,
      labelLetterSpacing: labelStyle.letterSpacing,
      objectiveLineHeight: objectiveStyle.lineHeight,
    };
  });
  expect(visualContract).toEqual({
    animationName: "none",
    borderRadius: "0px",
    borderWidth: "0px",
    boxShadow: "none",
    fontWeight: "400",
    labelLetterSpacing: "1px",
    objectiveLineHeight: "25px",
  });
});

test("複数目標を受信順で表示し長文をHUD幅内へ折り返す", async ({ page }) => {
  await setTopicScenario(page, "challengeMultiple");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.getByTestId("challenge-objective")).toHaveText([
    "石を採掘する",
    "石器をクラフトする",
    "木を伐採して拠点へ運ぶ",
  ]);
  await setTopicScenario(page, "challengeLong");
  const objective = page.getByTestId("challenge-objective");
  await expect(objective).toHaveCount(1);
  await expect(objective).toContainText("VeryLongUnbrokenChallengeObjectiveText");

  // 長語の複数行折返しを寸法検証する
  // Verify multiline wrapping of unbroken text through geometry
  const layout = await objective.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
      clientHeight: element.clientHeight,
      lineHeight: Number.parseFloat(style.lineHeight),
    };
  });
  expect(layout.scrollWidth).toBeLessThanOrEqual(layout.clientWidth);
  expect(layout.clientHeight / layout.lineHeight).toBeGreaterThan(1.5);
});

test("複数の長文目標を受信順かつHUD幅内で表示する", async ({ page }) => {
  await setTopicScenario(page, "challengeMultipleLong");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const multipleLongObjectives = page.getByTestId("challenge-objective");
  await expect(multipleLongObjectives).toHaveText([
    "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
    "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
    "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
  ]);

  // 各目標の折返しと横溢れを検証する
  // Verify each objective's wrapping and horizontal overflow
  const multipleLongLayouts = await multipleLongObjectives.evaluateAll((elements) => elements.map((element) => {
    const style = getComputedStyle(element);
    return {
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
      clientHeight: element.clientHeight,
      lineHeight: Number.parseFloat(style.lineHeight),
    };
  }));
  expect(multipleLongLayouts).toHaveLength(3);
  for (const multipleLongLayout of multipleLongLayouts) {
    expect(multipleLongLayout.scrollWidth).toBeLessThanOrEqual(multipleLongLayout.clientWidth);
    expect(multipleLongLayout.clientHeight / multipleLongLayout.lineHeight).toBeGreaterThan(1.5);
  }
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
