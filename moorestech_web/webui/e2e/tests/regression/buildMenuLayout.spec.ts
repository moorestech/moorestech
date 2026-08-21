// §8.11の寸法契約を固定する
// Locks the §8.11 dimension contract

import { test, expect } from "@playwright/test";
import { expectNoVerticalOverflow } from "../../support/layoutAssertions";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { buildMenuCategoryIds } from "../../mock-host/fixtures";

// 視覚寸法トークンをpx化する。calc()やremのまま返る生値を避け、実要素へ載せて解決させる
// Resolves a dimension token to px by letting a probe element compute it, since raw values may be calc() or rem
async function tokenPixels(page: import("@playwright/test").Page, name: string) {
  return page.evaluate((tokenName) => {
    const probe = document.createElement("div");
    probe.style.position = "absolute";
    probe.style.visibility = "hidden";
    probe.style.height = `var(${tokenName})`;
    document.body.appendChild(probe);
    const pixels = probe.getBoundingClientRect().height;
    probe.remove();
    return pixels;
  }, name);
}

// localeは共有状態のため既定に戻す
// Shared locale state; reset to default after
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "japanese");
  await setUiState(page, "PlayerInventory");
});

test("パネルは画面水平中央に表示される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const box = await page.getByTestId("build-menu-panel").boundingBox();
  const viewport = page.viewportSize();
  if (!box || !viewport) throw new Error("bounding box unavailable");
  expect(Math.abs(box.x + box.width / 2 - viewport.width / 2)).toBeLessThanOrEqual(1);
});

test("パネルは上部安全帯の直下にバンド高いっぱいで立つ", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // R2契約。期待値はトークンから解く
  // R2 contract; expectations derive from tokens
  const [panelWidth, upperSafeArea, contentHeight] = await Promise.all([
    tokenPixels(page, "--build-menu-panel-width"),
    tokenPixels(page, "--menu-upper-safe-area"),
    tokenPixels(page, "--menu-content-height"),
  ]);

  // stage枠からの相対で測る
  // Measure relative to the stage box
  const geometry = await page.getByTestId("build-menu-panel").evaluate((panel: HTMLElement) => {
    const stage = (panel.offsetParent as HTMLElement).offsetParent as HTMLElement;
    const panelRect = panel.getBoundingClientRect();
    const frameRect = panel.querySelector("[data-variant]")!.getBoundingClientRect();
    return {
      top: panelRect.top - stage.getBoundingClientRect().top,
      height: panelRect.height,
      width: panelRect.width,
      frameHeight: frameRect.height,
    };
  });

  // 丸め誤差吸収のため±1px許容
  // Allow +/-1px for rounding
  const scale = geometry.width / panelWidth;
  expect(Math.abs(geometry.top / scale - upperSafeArea)).toBeLessThanOrEqual(1);
  expect(Math.abs(geometry.height / scale - contentHeight)).toBeLessThanOrEqual(1);
  expect(Math.abs(geometry.frameHeight - geometry.height)).toBeLessThanOrEqual(1);
});

test("カテゴリボタンは全ボタン同一の固定高", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 期待値はトークンから算出
  // Expected value comes from the token
  const expected = await tokenPixels(page, "--build-menu-category-height");

  // offsetHeightで固定高を測る
  // Use offsetHeight; unaffected by stage scale
  // 丸めと小数トークンを吸収し±1px許容
  // Allow +/-1px for rounding and fractional tokens
  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  // 0件で無条件成功を防ぐ
  // Prevents a false pass when the count is zero
  const count = await buttons.count();
  expect(count).toBe(10);
  for (let i = 0; i < count; i += 1) {
    const height = await buttons.nth(i).evaluate((el: HTMLElement) => el.offsetHeight);
    expect(Math.abs(height - expected)).toBeLessThanOrEqual(1);
  }
});

test("カテゴリサイドバーは全カテゴリとラベルを固定高のまま収める", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 折返しはscrollHeightに出る
  // Wrapped labels surface via scrollHeight
  await expectNoVerticalOverflow(page.getByTestId("build-menu-sidebar").locator("button"));

  // パディング吸収を避け直接比較
  // Compares directly to avoid padding absorption
  const fit = await page.getByTestId("build-menu-columns").evaluate((columns: HTMLElement) => {
    const buttonList = columns.querySelectorAll('[data-testid="build-menu-sidebar"] button');
    const columnsRect = columns.getBoundingClientRect();
    // stage倍率でpaddingを揃える
    // Scale padding to match the stage
    const scale = columnsRect.height / columns.offsetHeight;
    const paddingBottom = parseFloat(getComputedStyle(columns).paddingBottom) * scale;
    const lastBottom = buttonList[buttonList.length - 1].getBoundingClientRect().bottom;
    return { lastBottom, contentBottom: columnsRect.bottom - paddingBottom };
  });
  expect(fit.lastBottom).toBeLessThanOrEqual(fit.contentBottom);
});

test("英語ロケールでもカテゴリ名は1行のまま固定高に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await setTopicScenario(page, "english");
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("data-locale", "english");

  // 最長英訳の表示を先に固定し誤検知防止
  // Pins the longest English name first to avoid a vacuous pass
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.buildingMaterial}`))
    .toHaveText("Building Materials");

  // フォント確定後に折返しを測る
  // Measure after web fonts settle
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await expectNoVerticalOverflow(page.getByTestId("build-menu-sidebar").locator("button"));
});

test("グリッドは8列を保ち中央列に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // SlotGridのprop値で列数検出
  // Column count comes straight from SlotGrid's prop
  const grid = page.locator('[data-testid^="build-menu-grid-"]').first();
  const columnCount = await grid.evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(columnCount).toBe(8);

  // 8列の実幅を収容幅と比較
  // Compares the 8-column width against capacity
  const fit = await page.getByTestId("build-menu-sections").evaluate((area: HTMLElement) => {
    const style = getComputedStyle(area);
    const reserve = parseFloat(style.paddingRight);
    return {
      reserve,
      contentWidth: area.clientWidth - parseFloat(style.paddingLeft) - reserve,
      gridWidth: area.querySelector<HTMLElement>('[data-testid^="build-menu-grid-"]')!.offsetWidth,
    };
  });
  // 予約もスクロールバー重なり防止で固定
  // Pins the reserve too, to avoid the scrollbar overlapping column 8
  expect(fit.reserve).toBeGreaterThan(0);
  expect(fit.gridWidth).toBeLessThanOrEqual(fit.contentWidth);
});
