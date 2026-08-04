import { test, expect } from "@playwright/test";
import { setSkitStage, setTopicScenario, setUiState } from "../../support/mockControl";
import { expectAbove, expectAtViewportTopCorner, expectNoHorizontalOverflow } from "../../support/layoutAssertions";
import { expectChallengeHudPresentation, expectWrappedObjectives, readChallengeHudPresentation } from "../../support/challengeHudAssertions";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "challengeActive");
  await setSkitStage(page, "none");
  await setUiState(page, "PlayerInventory");
  await setTopicScenario(page, "japanese");
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

  // 左上固定の寸法と短い罫線を検証する
  // Verify top-left fixed dimensions and the shortened rule
  await expect(hud).toHaveCSS("top", "24px");
  await expect(hud).toHaveCSS("left", "24px");
  await expect(hud).toHaveCSS("width", "520px");
  await expect(hud).toHaveCSS("text-shadow", "rgba(0, 0, 0, 0.85) 0px 1px 2px");
  const rule = hud.locator('[aria-hidden="true"]');
  await expect(rule).toHaveCount(1);
  await expect(rule).toHaveCSS("width", "176px");

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
    objectiveLineHeight: "20px",
  });
});

test("横長画面で進行HUDを実画面左上へ置き罫線を約3分の1へ縮める", async ({ page }) => {
  await page.setViewportSize({ width: 2432, height: 786 });
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");

  const hud = page.getByTestId("challenge-hud");
  await expectAtViewportTopCorner(hud, "left", 40);
  const hudBox = await hud.boundingBox();
  const ruleBox = await hud.locator('[aria-hidden="true"]').boundingBox();
  expect(hudBox).not.toBeNull();
  expect(ruleBox).not.toBeNull();
  expect(hudBox!.width).toBeCloseTo(520 * 786 / 720, 1);
  expect(ruleBox!.width).toBeCloseTo(176 * 786 / 720, 1);
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
  await expectWrappedObjectives(objective, 1);
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

  await expectNoHorizontalOverflow(multipleLongObjectives);
  const gamePresentation = await readChallengeHudPresentation(page);
  await setUiState(page, "PlayerInventory");
  const inventoryHud = page.getByTestId("challenge-hud");
  await expectChallengeHudPresentation(page, gamePresentation);
  await expectAbove(inventoryHud, page.getByRole("heading", { name: "持ち物" }));
});
