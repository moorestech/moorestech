import { expect, test } from "@playwright/test";
import { setUiState } from "../../support/mockControl";

// 各テスト後に既定状態へ戻し、他 spec へ画面状態を漏らさない
// Reset to defaults after each test so screen state never leaks into other specs
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

// パネル選択不可・入力欄のみ選択可を確認
// Assert panel text unselectable, inputs selectable
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
