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

test("機械レシピ3件を表示し、解除と再選択をblock topicへ反映する", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-tab-recipes").click();

  const selection = page.getByTestId("machine-recipe-selection");
  const slots = selection.locator('[data-testid^="machine-recipe-"]:not([data-testid^="machine-recipe-detail"])');
  await expect(selection).toBeVisible();
  await expect(slots).toHaveCount(3);
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(1);
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");

  // 選択中レシピの材料詳細と所要時間が表示される
  // The selected recipe shows its ingredient detail and time
  await expect(page.getByTestId("machine-recipe-detail")).toBeVisible();
  await expect(page.getByTestId("machine-recipe-detail-time")).toContainText("10");

  await page.getByTestId(selectedRecipeTestId).click({ button: "right" });
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(0);
  await expect(page.getByTestId("machine-recipe-detail")).toHaveCount(0);

  await page.getByTestId(firstRecipeTestId).click();
  await expect(page.getByTestId(firstRecipeTestId)).toHaveAttribute("data-selected", "true");
  await expect(page.getByTestId("machine-recipe-detail-time")).toContainText("5");
});

test("レシピ無しブロックは小型パネルのままタブを出さない", async ({ page }) => {
  await setBlock(page, "generator");
  await page.goto("/");

  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByTestId("block-inventory")).not.toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-tab-switch")).toHaveCount(0);
});
