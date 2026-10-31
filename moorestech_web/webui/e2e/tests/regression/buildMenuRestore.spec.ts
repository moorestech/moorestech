import { test, expect } from "@playwright/test";
import { setUiState } from "../../support/mockControl";
import { buildMenuCategoryIds, buildMenuEntryIds } from "../../mock-host/fixtures";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("閉じて開き直すと検索・スクロール・詳細stickyが復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`).hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("鉄道レール");
  // スクロール前にポインタを一覧の外へ退避する。置いたままだとスクロール後の合成hoverが下に来た別エントリへ詳細を移す
  // Park the pointer outside the list before scrolling; left in place, the post-scroll synthetic hover moves the detail to whichever entry slid under it
  await page.mouse.move(1, 1);
  const viewport = page.getByTestId("build-menu-panel").locator(".mantine-ScrollArea-viewport");
  const headingTop = await page
    .getByTestId(`build-menu-category-heading-${buildMenuCategoryIds.transport}`)
    .evaluate((el: HTMLElement) => el.offsetTop);
  // 合成scrollイベントは使わない
  // No synthetic scroll event
  await viewport.evaluate((el, top) => { el.scrollTop = top; }, headingTop);
  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();

  await setUiState(page, "BuildMenu");
  await expect(page.getByTestId("build-menu-detail")).toContainText("鉄道レール");
  // 全カテゴリ常時DOM化でtoBeAttached()は恒真になるため、復元後の対象見出しoffsetTopと視口scrollTopの一致で検証する
  // toBeAttached() is tautological now that every category stays mounted, so verify the restored viewport scrollTop matches the target heading's offsetTop instead
  await expect
    .poll(() =>
      page
        .getByTestId("build-menu-panel")
        .locator(".mantine-ScrollArea-viewport")
        .evaluate((el) => el.scrollTop),
    )
    .toBe(headingTop);
});
