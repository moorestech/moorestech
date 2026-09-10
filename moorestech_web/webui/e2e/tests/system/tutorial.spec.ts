import { expect, test, type Locator } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

const expectedOutlineBoxShadow = "rgba(255, 0, 0, 0.24) 0px 0px 0px 4px";

// 実ブラウザで再生中の脈動を計測する。CSS文字列一致では拾えない無言死をここで捕まえる
// Measures the pulse actually playing in a real browser; a silent death that CSS text matching misses is caught here
function measurePulse(locator: Locator) {
  return locator.evaluate((element) => {
    const style = getComputedStyle(element);
    const transforms = element.getAnimations().flatMap((animation) =>
      (animation.effect as KeyframeEffect).getKeyframes().map((frame) => String(frame.transform)));
    return {
      name: style.animationName, duration: style.animationDuration,
      iterationCount: style.animationIterationCount, transforms,
    };
  });
}

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
});

test("tutorial outline highlights the target without dimming the rest of the screen", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutline");

  const overlay = page.getByTestId("tutorial-overlay");
  const highlight = overlay.locator("[data-kind]");
  await expect(highlight).toBeVisible();
  await expect(highlight).toHaveAttribute("data-kind", "outline");
  await expect(highlight).toHaveCSS("border-top-color", "rgb(255, 0, 0)");
  await expect(highlight).toHaveCSS("box-shadow", expectedOutlineBoxShadow);
  await expect(overlay).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  await expect(overlay).toHaveCSS("background-image", "none");
  await expect(overlay).toHaveCSS("filter", "none");
  await expect(overlay).toHaveCSS("backdrop-filter", "none");

  // 廃止済みkindへの変更でも暗転CSSが復活しないことを直接検査する
  // Probe the removed kind directly so restored dimming CSS cannot escape the test
  await highlight.evaluate((element) => element.setAttribute("data-kind", "spotlight"));
  await expect(highlight).toHaveCSS("box-shadow", expectedOutlineBoxShadow);
});

test("outline with a label renders the label text beside the outline", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutlineWithLabel");
  const label = page.getByTestId("tutorial-highlight-label");
  await expect(label).toBeVisible();
  await expect(label).toHaveText("照準に合わせる");
});

test("keyControl hint renders a kbd and text above the hotbar while uiState matches", async ({ page }) => {
  await page.goto("/");
  await setUiState(page, "GameScreen");
  await setTopicScenario(page, "tutorialKeyControl");
  const hint = page.getByTestId("key-control-hint");
  await expect(hint).toBeVisible();
  await expect(hint.locator("kbd")).toHaveText("Tab");
  await expect(hint).toContainText("インベントリを開く");
});

test("keyControl hint actually plays the strong 1.08 pulse in the browser", async ({ page }) => {
  await page.goto("/");
  await setUiState(page, "GameScreen");
  await setTopicScenario(page, "tutorialKeyControl");
  const hint = page.getByTestId("key-control-hint");
  await expect(hint).toBeVisible();

  // 素名で出るのはキーフレームがtokens.cssのグローバル側にあるため。ここが崩れたら書き換え事故
  // The raw name appears because the keyframes live in global tokens.css; a broken name means a rewrite accident
  const pulse = await measurePulse(hint);
  expect(pulse.name).toBe("tutorial-attention-pulse-strong");
  expect(pulse.duration).toBe("1.2s");
  expect(pulse.iterationCount).toBe("infinite");
  expect(pulse.transforms).toContain("scale(1.08)");
});

test("tutorial outline actually plays the subtle 1.03 pulse in the browser", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutline");
  const outline = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  await expect(outline).toBeVisible();

  // 枠はヒントより控えめな振幅。強弱の取り違えもここで落ちる
  // The ring pulses more gently than the hint; swapping the two amplitudes fails here as well
  const pulse = await measurePulse(outline);
  expect(pulse.name).toBe("tutorial-attention-pulse-subtle");
  expect(pulse.duration).toBe("1.2s");
  expect(pulse.iterationCount).toBe("infinite");
  expect(pulse.transforms).toContain("scale(1.03)");
});

// 枠線の撤去がフレーム到来に依存すると、CEF(外部BeginFrame駆動)ではフレーム間隔ぶんゴーストが残る
// If the outline's teardown waits for a frame, CEF (driven by external BeginFrame) shows a ghost for that whole gap
test("outline never outlives its anchor even when no frame arrives", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await setTopicScenario(page, "tutorialRecipeItem");

  const grid = page.getByTestId("item-list-grid");
  const outline = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  await expect(grid).toBeVisible();
  await expect(outline).toBeVisible();

  // rAFを止めて「次の描画が来ない」状況を作る。React本体はMessageChannel駆動なので進み続ける
  // Stop rAF to model "no next frame"; React itself is MessageChannel-driven and keeps advancing
  await page.evaluate(() => {
    window.requestAnimationFrame = (() => 1) as typeof window.requestAnimationFrame;
  });
  // 差し替え時点で予約済みだった本物のrAFを消化しきってから閉じる。残っていると偶然そちらが撤去してしまう
  // Let the real rAF already queued at swap time drain first; otherwise it may do the teardown by luck
  await page.waitForTimeout(200);

  await setUiState(page, "GameScreen");

  await expect(grid).toHaveCount(0);
  await expect(outline).toHaveCount(0);
});
