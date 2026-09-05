import { expect, test, type Page } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";
import { scrollAreaRootOf, scrollAreaViewport, scrollAreaVerticalBar, expectScrollsOnlyWhenOverflowing } from "../../support/layoutAssertions";

// レシピ単一リストのスクロール領域も、大きさは器（パネル本文）が決める（§8.10・ユーザー裁定 2026-08-22）。
// 件数でパネルごと伸びると「溢れた時だけスクロール」が成立せず、クリップがエントリ外周へ届かず枠が削られる
// The recipe list's scroller is sized by its container too (§8.10, user ruling 2026-08-22); letting the panel grow with the
// recipe count breaks "scroll only once it overflows" and pulls the clip inside the entries, shaving the tutorial ring

const craftPanel = (page: Page) => page.locator('[data-variant="craft"]');
const scrollRoot = (page: Page) => scrollAreaRootOf(page, "recipe-entry-list");
const viewport = (page: Page) => scrollAreaViewport(scrollRoot(page));

const openPlankRecipes = async (page: Page) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await expect(page.getByTestId("recipe-entry-list")).toBeVisible();
};

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "machineRecipesDefault");
});

test("レシピが溢れてもパネル高は変わらず、縦バーだけが出る", async ({ page }) => {
  await openPlankRecipes(page);
  await expectScrollsOnlyWhenOverflowing(
    scrollRoot(page),
    craftPanel(page),
    () => setTopicScenario(page, "machineRecipesOverflow"),
  );
});

test("クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる", async ({ page }) => {
  await openPlankRecipes(page);
  await setTopicScenario(page, "machineRecipesOverflow");
  await expect(scrollAreaVerticalBar(scrollRoot(page))).toBeVisible();

  // リテラルでなく関係式で見る。マスタのpaddingPxが変わってもこの検査が番人であり続けるため
  // Assert the relation rather than literals so this stays a guard even when the master's paddingPx changes
  const clearanceOf = () => viewport(page).evaluate((element) => {
    const entries = element.querySelectorAll('[data-testid^="craft-recipe-entry"], [data-testid^="machine-recipe-entry"]');
    const clip = element.getBoundingClientRect();
    const first = entries[0].getBoundingClientRect();
    const last = entries[entries.length - 1].getBoundingClientRect();
    const root = getComputedStyle(document.documentElement);
    const readPx = (name: string) => Number.parseFloat(root.getPropertyValue(name));
    return {
      left: first.left - clip.left, right: clip.right - first.right,
      top: first.top - clip.top, bottom: clip.bottom - last.bottom,
      required: readPx("--tutorial-anchor-padding") + readPx("--tutorial-highlight-glow"),
    };
  });

  const atTop = await clearanceOf();
  expect(atTop.required).toBeGreaterThan(0);
  expect(atTop.top).toBeGreaterThanOrEqual(atTop.required);
  expect(atTop.left).toBeGreaterThanOrEqual(atTop.required);
  expect(atTop.right).toBeGreaterThanOrEqual(atTop.required);

  // 最下段まで送っても最終エントリの下に逃げが残る
  // Scrolled to the end, the last entry still keeps its clearance
  await viewport(page).evaluate((element) => { element.scrollTop = element.scrollHeight; });
  const atBottom = await clearanceOf();
  expect(atBottom.bottom).toBeGreaterThanOrEqual(atBottom.required);
});
