import { expect, type Locator, type Page } from "@playwright/test";

type ChallengeHudPresentation = {
  html: string;
  className: string;
  childCount: number;
  ariaLabel: string | null;
  rect: { x: number; y: number; width: number; height: number };
  label: { fontSize: string; lineHeight: string; letterSpacing: string };
  objectives: Array<{ fontSize: string; lineHeight: string; gap: string }>;
};

// 画面間比較用にHUD描画契約を一括取得
// Read the HUD presentation contract for cross-screen comparison
export async function readChallengeHudPresentation(page: Page): Promise<ChallengeHudPresentation> {
  const hud = page.getByTestId("challenge-hud");
  await expect(hud).toBeVisible();
  return hud.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    const label = element.querySelector<HTMLElement>('[data-testid="challenge-hud-label"]')!;
    const objectives = Array.from(element.querySelectorAll<HTMLElement>('[data-testid="challenge-objective"]'));
    const objectiveContainer = objectives[0]?.parentElement!;
    return {
      html: element.outerHTML,
      className: element.className,
      childCount: element.childElementCount,
      ariaLabel: element.getAttribute("aria-label"),
      rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
      label: {
        fontSize: getComputedStyle(label).fontSize,
        lineHeight: getComputedStyle(label).lineHeight,
        letterSpacing: getComputedStyle(label).letterSpacing,
      },
      objectives: objectives.map((objective) => ({
        fontSize: getComputedStyle(objective).fontSize,
        lineHeight: getComputedStyle(objective).lineHeight,
        gap: getComputedStyle(objectiveContainer).gap,
      })),
    };
  });
}

// UIStateが変わってもチャレンジHUDのDOMと文字組は完全一致させる
// Keep challenge HUD DOM and typography identical across UIState changes
export async function expectChallengeHudPresentation(page: Page, expected: ChallengeHudPresentation) {
  await expect(readChallengeHudPresentation(page)).resolves.toEqual(expected);
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
