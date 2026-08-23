import { expect, test, type Page } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";

// レシピ単一リストのスクロール領域も、大きさは器（パネル本文）が決める（§8.10・ユーザー裁定 2026-08-22）。
// 件数でパネルごと伸びると「溢れた時だけスクロール」が成立せず、クリップがエントリ外周へ届かず枠が削られる
// The recipe list's scroller is sized by its container too (§8.10, user ruling 2026-08-22); letting the panel grow with the
// recipe count breaks "scroll only once it overflows" and pulls the clip inside the entries, shaving the tutorial ring

const craftPanel = (page: Page) => page.locator('[data-variant="craft"]');
const scrollRoot = (page: Page) =>
  page.getByTestId("recipe-entry-list").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
const verticalBar = (page: Page) => scrollRoot(page).locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]');
const viewport = (page: Page) => scrollRoot(page).locator(".mantine-ScrollArea-viewport");

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

  // 既定fixtureは溢れないため、バーは出ずスクロール量も0
  // The default fixture does not overflow, so no bar appears and there is nothing to scroll
  const settledHeight = (await craftPanel(page).boundingBox())!.height;
  await expect(verticalBar(page)).toBeHidden();
  expect(await viewport(page).evaluate((element) => element.scrollHeight - element.clientHeight)).toBe(0);

  // 溢れる件数でもパネルは伸びない。伸びるならスクロールが始まらない
  // An overflowing count must not grow the panel; if it grows, scrolling never starts
  await setTopicScenario(page, "machineRecipesOverflow");
  await expect(verticalBar(page)).toBeVisible();
  expect((await craftPanel(page).boundingBox())!.height).toBeCloseTo(settledHeight, 1);
  expect(await viewport(page).evaluate((element) => element.scrollHeight - element.clientHeight)).toBeGreaterThan(0);
});

test("クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる", async ({ page }) => {
  await openPlankRecipes(page);
  await setTopicScenario(page, "machineRecipesOverflow");
  await expect(verticalBar(page)).toBeVisible();

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
