import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setSkitStage, setUiState } from "../support/mockControl";

test.beforeEach(async ({ page }) => {
  await setSkitStage(page, "none");
});

test.afterEach(async ({ page }) => {
  await setSkitStage(page, "none");
  await setUiState(page, "GameScreen");
});

test("blocking skit reveals, advances, and selects by choiceId", async ({ page }) => {
  const advanceCountBefore = (await payloadsOf(page, "skit.advance")).length;
  await page.goto("/");
  await setSkitStage(page, "text");
  const skit = page.getByTestId("blocking-skit");
  await expect(skit).toBeVisible();

  // 本文表示中のクリックはWeb内revealだけでUnity actionを送らない
  // A click during typing only reveals locally and sends no Unity action
  await skit.click();
  await expect(skit).toContainText("Blocking message");
  expect((await payloadsOf(page, "skit.advance")).length).toBe(advanceCountBefore);

  // 全文表示後のクリックでadvanceし、mock hostが選択肢snapshotへ進める
  // A click after full reveal advances and the mock host moves to the choice snapshot
  await skit.click();
  await expect(page.getByRole("button", { name: "Route B" })).toBeVisible();
  await page.getByRole("button", { name: "Route B" }).click();

  await expect(page.getByTestId("blocking-skit")).toHaveCount(0);
  await expect.poll(async () => {
    const values = await payloadsOf(page, "skit.select");
    return values[values.length - 1];
  }).toEqual({ sessionId: "blocking-1", sceneRevision: 2, choiceId: "route-b" });
});

test("横長画面で会話帯を全幅に広げツールを実画面右上へ固定する", async ({ page }) => {
  await page.setViewportSize({ width: 2432, height: 786 });
  await page.goto("/");
  await setSkitStage(page, "text");

  const skit = page.getByTestId("blocking-skit");
  const hideUiButton = page.getByRole("button", { name: "Hide UI" });
  await expect(skit).toBeVisible();
  await expect(hideUiButton).toBeVisible();

  // 横長帯と操作視認性を実寸確認する
  // Catch regressions that collapse back to stage width or lose tool contrast over a bright world
  const layout = await page.evaluate(() => {
    const skitRect = document.querySelector<HTMLElement>('[data-testid="blocking-skit"]')!.getBoundingClientRect();
    const tool = document.querySelector<HTMLElement>('button[aria-label="Hide UI"]')!;
    const toolRect = tool.getBoundingClientRect();
    const toolStyle = getComputedStyle(tool);
    return {
      skitLeft: skitRect.left,
      skitRight: skitRect.right,
      skitBottom: skitRect.bottom,
      toolRightGap: window.innerWidth - toolRect.right,
      toolTop: toolRect.top,
      toolFilter: toolStyle.filter,
      toolOpacity: toolStyle.opacity,
      viewportWidth: window.innerWidth,
      viewportHeight: window.innerHeight,
    };
  });
  expect(layout.skitLeft).toBeCloseTo(0, 1);
  expect(layout.skitRight).toBeCloseTo(layout.viewportWidth, 1);
  expect(layout.skitBottom).toBeCloseTo(layout.viewportHeight, 1);
  expect(layout.toolRightGap).toBeGreaterThanOrEqual(0);
  expect(layout.toolRightGap).toBeLessThan(40);
  expect(layout.toolTop).toBeGreaterThanOrEqual(0);
  expect(layout.toolTop).toBeLessThan(20);
  expect(layout.toolFilter).not.toBe("none");
  expect(layout.toolOpacity).toBe("1");
});

test("Escでポーズメニューを開いても会話UIは背後で表示され続ける", async ({ page }) => {
  await page.goto("/");
  await setSkitStage(page, "text");
  await setUiState(page, "Story", "PauseMenu");

  await expect(page.getByTestId("pause-menu")).toBeVisible();
  await expect(page.getByTestId("blocking-skit")).toBeVisible();

  await setUiState(page, "Story", "Playing");
  await expect(page.getByTestId("pause-menu")).toHaveCount(0);
});
