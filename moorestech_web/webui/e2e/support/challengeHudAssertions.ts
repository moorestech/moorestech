import { expect, type Locator, type Page } from "@playwright/test";
import { expectCenteredHorizontally, expectNoHorizontalOverflow } from "./layoutAssertions";

export async function expectCompactMenuChallengeHud(page: Page) {
  const hud = page.getByTestId("challenge-hud");
  const objectives = page.getByTestId("challenge-objective");
  await expect(hud).toBeVisible();
  await expectCenteredHorizontally(hud, page.locator("body"));
  await expect(objectives).toHaveCount(3);
  await expectNoHorizontalOverflow(objectives);
  await expect(objectives.first()).toHaveCSS("font-size", "14px");
  await expect(objectives.first()).toHaveCSS("line-height", "20px");
}

// 各目標が自身の行幅に収まり複数行へ折り返すことを検証する
// Verify that every objective fits its line width and wraps across multiple lines
export async function expectWrappedObjectives(objectives: Locator, expectedCount: number) {
  const layouts = await objectives.evaluateAll((elements) => elements.map((element) => {
    const style = getComputedStyle(element);
    return {
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
      clientHeight: element.clientHeight,
      lineHeight: Number.parseFloat(style.lineHeight),
    };
  }));
  expect(layouts).toHaveLength(expectedCount);
  for (const layout of layouts) {
    expect(layout.scrollWidth).toBeLessThanOrEqual(layout.clientWidth);
    expect(layout.clientHeight / layout.lineHeight).toBeGreaterThan(1.5);
  }
}
