import { expect, test, type Page } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";
import { scrollAreaRootOf, scrollAreaViewport, scrollAreaVerticalBar } from "../../support/layoutAssertions";

// スクロール領域はパネル本文いっぱいで、内容ぴったりに縮まない（ユーザー裁定 2026-08-22）。
// 縮むと(1)溢れていないのにスクロール扱いになり(2)クリップがセル外周へ届かずチュートリアルのラベルが落ちる
// The scroller fills the panel body instead of hugging its content (user ruling 2026-08-22); hugging would
// (1) treat a non-overflowing list as a scroller and (2) pull the clip inside the cell, dropping the tutorial label

const scrollRoot = (page: Page) => scrollAreaRootOf(page, "item-list-grid");
const verticalBar = (page: Page) => scrollAreaVerticalBar(scrollRoot(page));
const horizontalBar = (page: Page) => scrollRoot(page).locator('.mantine-ScrollArea-scrollbar[data-orientation="horizontal"]');
const viewport = (page: Page) => scrollAreaViewport(scrollRoot(page));
const label = (page: Page) => page.getByTestId("tutorial-highlight-label");

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
  await setTopicScenario(page, "itemListDefault");
});

test("スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();

  // クリップ境界(viewport)がパネル本文いっぱいまで届いていること。内容1段ぶんで止まっていないこと
  // 下端は逃げ(--tutorial-anchor-clip-inset)ぶんだけ本文の外へ出る
  // The clip edge (viewport) spans the whole panel body instead of stopping at the single content row;
  // its bottom sits outside the body by the clearance (--tutorial-anchor-clip-inset)
  const geometry = await viewport(page).evaluate((element) => {
    const body = element.closest("[data-variant='default']")!.querySelector("[class*='_body_']")!;
    return {
      viewportBottom: element.getBoundingClientRect().bottom,
      bodyBottom: body.getBoundingClientRect().bottom,
      gridBottom: element.querySelector('[data-testid="item-list-grid"]')!.getBoundingClientRect().bottom,
      overflowY: element.scrollHeight - element.clientHeight,
      overflowX: element.scrollWidth - element.clientWidth,
    };
  });
  expect(geometry.viewportBottom - geometry.bodyBottom).toBeCloseTo(12, 0);
  expect(geometry.viewportBottom - geometry.gridBottom).toBeGreaterThan(100);
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

test("クリップ境界はグリッドからハイライトの逃げぶん離れ、枠が削られない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();

  // 逃げはマスタのpaddingPx + グロー以上。下回るとセルがクリップ端に密着し枠が「コ」の字に欠ける
  // The clearance is at least the master's paddingPx + glow; below it the cell hugs the clip edge and the ring is notched
  // リテラルでなく関係式で見る。マスタのpaddingPxが変わってもこの検査が番人であり続けるため
  // Assert the relation rather than literals so this stays a guard even when the master's paddingPx changes
  const clearance = await viewport(page).evaluate((element) => {
    const grid = element.querySelector('[data-testid="item-list-grid"]')!.getBoundingClientRect();
    const clip = element.getBoundingClientRect();
    const root = getComputedStyle(document.documentElement);
    const readPx = (name: string) => Number.parseFloat(root.getPropertyValue(name));
    return {
      top: grid.top - clip.top, left: grid.left - clip.left, right: clip.right - grid.right,
      required: readPx("--tutorial-anchor-padding") + readPx("--tutorial-highlight-glow"),
    };
  });
  expect(clearance.required).toBeGreaterThan(0);
  expect(clearance.top).toBeGreaterThanOrEqual(clearance.required);
  expect(clearance.left).toBeGreaterThanOrEqual(clearance.required);
  expect(clearance.right).toBeGreaterThanOrEqual(clearance.required);

  // 枠は四辺とも削られない(insetは負=グロー分だけ外側へ出る)
  // No side of the ring is shaved; a negative inset means it extends outward by the glow
  await setTopicScenario(page, "tutorialRecipeItem");
  const outline = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  await expect(outline).toBeVisible();
  await expect(outline).toHaveCSS("clip-path", "inset(-4px)");
});

test("7段は溢れず、最下段までスクロールしても最終段に逃げが残る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();

  // 溢れる直前の7段でもバーを出さない
  // Seven rows is the last non-overflowing count and must not raise a bar
  await setTopicScenario(page, "itemListSevenRows");
  await expect(verticalBar(page)).toBeHidden();

  // 溢れる件数で最下段までスクロールしても、最終段の下に逃げが残り枠が削られない
  // Even scrolled to the end of an overflowing list the last row keeps its clearance so the ring survives
  await setTopicScenario(page, "itemListLarge");
  await expect(verticalBar(page)).toBeVisible();
  await viewport(page).evaluate((element) => { element.scrollTop = element.scrollHeight; });
  const bottom = await viewport(page).evaluate((element) => {
    const cells = element.querySelectorAll("[data-item-id]");
    return element.getBoundingClientRect().bottom - cells[cells.length - 1].getBoundingClientRect().bottom;
  });
  expect(bottom).toBeGreaterThanOrEqual(12);
});
