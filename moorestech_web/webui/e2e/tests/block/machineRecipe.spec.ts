import { test, expect } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("レシピ有り機械は大型パネルでインベントリ/レシピ選択タブを切り替える", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");

  // レシピ有り機械は大型パネル、デフォルトはインベントリタブ
  // Recipe-capable machines get the large panel; the inventory tab is the default
  await expect(page.getByTestId("block-inventory")).toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-tab-switch")).toBeVisible();
  await expect(page.getByTestId("machine-tab-inventory")).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId("machine-input-slots")).toBeVisible();
  await expect(page.getByTestId("machine-recipe-selection")).toHaveCount(0);
  // 選択中レシピの生産物がインベントリタブにも1個表示される
  // The selected recipe's product also shows on the inventory tab as one slot
  await expect(page.getByTestId("machine-selected-product")).toBeVisible();
  // 電力率はタブ外の共通フッタとして常時表示される
  // The power rate stays visible as a common footer outside the tabs
  await expect(page.getByTestId("machine-power-rate")).toBeVisible();

  await page.getByTestId("machine-tab-recipes").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
  await expect(page.getByTestId("machine-input-slots")).toHaveCount(0);
  await expect(page.getByTestId("machine-power-rate")).toBeVisible();

  await page.getByTestId("machine-tab-inventory").click();
  await expect(page.getByTestId("machine-input-slots")).toBeVisible();
});

test("機械レシピ3件を表示し、ホバー詳細・解除・選択時のタブ遷移を反映する", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-tab-recipes").click();

  const selection = page.getByTestId("machine-recipe-selection");
  const slots = selection.locator('[data-testid^="machine-recipe-"]:not([data-testid^="machine-recipe-detail"])');
  await expect(selection).toBeVisible();
  await expect(slots).toHaveCount(3);
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(1);
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");

  // 既定の詳細プレビューは選択中レシピ。ホバー中はホバー先を優先する
  // The default detail preview is the selected recipe; hover takes precedence
  await expect(page.getByTestId("machine-recipe-detail")).toBeVisible();
  await expect(page.getByTestId("machine-recipe-detail-time")).toContainText("10");
  await page.getByTestId(firstRecipeTestId).hover();
  await expect(page.getByTestId("machine-recipe-detail-time")).toContainText("5");
  await page.getByTestId("machine-recipe-detail").hover();
  await expect(page.getByTestId("machine-recipe-detail-time")).toContainText("10");

  // 右クリック解除後、ホバーを外すと詳細は案内文へ戻る
  // After right-click clearing, leaving hover swaps the detail for the guidance text
  await page.getByTestId(selectedRecipeTestId).click({ button: "right" });
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(0);
  await page.getByTestId("machine-recipe-detail").hover();
  await expect(page.getByTestId("machine-recipe-detail")).toHaveCount(0);
  await expect(page.getByTestId("machine-recipe-detail-empty")).toBeVisible();

  // 左クリック選択で選択が反映され、インベントリタブへ自動遷移する
  // Left-click selection applies and automatically jumps to the inventory tab
  await page.getByTestId(firstRecipeTestId).click();
  await expect(page.getByTestId("machine-tab-inventory")).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId("machine-input-slots")).toBeVisible();
  await expect(page.getByTestId("machine-selected-product")).toBeVisible();
});

test("レシピ無しブロックは小型パネルのままタブを出さない", async ({ page }) => {
  await setBlock(page, "generator");
  await page.goto("/");

  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByTestId("block-inventory")).not.toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-tab-switch")).toHaveCount(0);
});
