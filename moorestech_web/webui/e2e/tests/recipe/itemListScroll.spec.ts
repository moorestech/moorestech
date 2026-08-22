import { expect, test, type Page } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";

// スクロール領域はパネル本文いっぱいで、内容ぴったりに縮まない（ユーザー裁定 2026-08-22）。
// 縮むと(1)溢れていないのにスクロール扱いになり(2)クリップがセル外周へ届かずチュートリアルのラベルが落ちる
// The scroller fills the panel body instead of hugging its content (user ruling 2026-08-22); hugging would
// (1) treat a non-overflowing list as a scroller and (2) pull the clip inside the cell, dropping the tutorial label

const scrollRoot = (page: Page) =>
  page.getByTestId("item-list-grid").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
const verticalBar = (page: Page) => scrollRoot(page).locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]');
const horizontalBar = (page: Page) => scrollRoot(page).locator('.mantine-ScrollArea-scrollbar[data-orientation="horizontal"]');
const viewport = (page: Page) => scrollRoot(page).locator(".mantine-ScrollArea-viewport");
const label = (page: Page) => page.getByTestId("tutorial-highlight-label");

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
  await setTopicScenario(page, "itemListDefault");
});

test("スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();

  // クリップ境界(viewport)がパネル本文の下端まで届いていること。内容1段ぶんで止まっていないこと
  // The clip edge (viewport) reaches the panel body's bottom instead of stopping at the single content row
  const geometry = await viewport(page).evaluate((element) => {
    const body = element.closest("[data-variant='default']")!.querySelector("[class*='_body_']")!;
    return {
      viewportBottom: element.getBoundingClientRect().bottom,
      bodyBottom: body.getBoundingClientRect().bottom,
      contentHeight: element.firstElementChild!.getBoundingClientRect().height,
      overflowY: element.scrollHeight - element.clientHeight,
      overflowX: element.scrollWidth - element.clientWidth,
    };
  });
  expect(geometry.viewportBottom).toBeCloseTo(geometry.bodyBottom, 0);
  expect(geometry.viewportBottom - geometry.contentHeight).toBeGreaterThan(100);
  expect(geometry).toMatchObject({ overflowY: 0, overflowX: 0 });
  await expect(verticalBar(page)).toBeHidden();
  await expect(horizontalBar(page)).toBeHidden();
});

test("パネルを超える件数で初めて縦バーが出る。横は常に出ない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await expect(verticalBar(page)).toBeHidden();

  await setTopicScenario(page, "itemListLarge");
  await expect(verticalBar(page)).toBeVisible();
  // 横は列数固定で溢れないため、件数が増えても水平バーは出さない
  // The column count is fixed so nothing overflows horizontally, however many items arrive
  await expect(horizontalBar(page)).toBeHidden();
});

test("溢れていない一覧の最上段でもチュートリアルのラベルが出る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await setTopicScenario(page, "tutorialRecipeItem");

  await expect(page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]')).toBeVisible();
  await expect(label(page)).toBeVisible();
});

test("アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await setTopicScenario(page, "tutorialRecipeItem");
  await setTopicScenario(page, "itemListLarge");

  const outline = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  // 溢れていてもスクロール前なら最上段は完全に見えており、ラベルは出たまま
  // Even while overflowing, the top row is fully visible before scrolling, so the label stays
  await expect(outline).toBeVisible();
  await expect(label(page)).toBeVisible();

  const scrollTo = (top: number) => viewport(page).evaluate((element, value) => { element.scrollTop = value; }, top);

  // 一部でも隠れた時点でラベルは引っ込む(枠はクリップされて残る)
  // The label retreats as soon as any part hides, while the outline stays and gets clipped
  await scrollTo(20);
  await expect(label(page)).toHaveCount(0);
  await expect(outline).toBeVisible();

  // 完全に隠れたら枠ごと描かない(ADR 0024)
  // Once fully hidden the outline is not drawn at all (ADR 0024)
  await scrollTo(300);
  await expect(outline).toHaveCount(0);
});
