import { expect, test } from "@playwright/test";
import { setUiState } from "../../support/mockControl";

// 選択可否の値源はグローバル1箇所。パネル文字は選択不可・入力欄だけ選択可を実画面で固定する
// The selection policy lives in one global place; assert unselectable panel text and selectable inputs in the real page
test("入力欄以外はテキスト選択できない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const heading = page.getByRole("heading", { name: "持ち物" });
  await expect(heading).toBeVisible();
  const headingUserSelect = await heading.evaluate((element) => getComputedStyle(element).userSelect);
  expect(headingUserSelect).toBe("none");

  const bodyUserSelect = await page.evaluate(() => getComputedStyle(document.body).userSelect);
  expect(bodyUserSelect).toBe("none");
});

test("建設メニューの検索入力はテキスト選択できる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const searchInput = page.getByTestId("build-menu-search");
  await expect(searchInput).toBeVisible();
  const inputUserSelect = await searchInput.evaluate((element) => getComputedStyle(element).userSelect);
  expect(inputUserSelect).toBe("text");
});
