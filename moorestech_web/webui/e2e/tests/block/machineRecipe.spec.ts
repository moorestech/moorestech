import { test, expect, type Page } from "@playwright/test";
import { setBlock, setTopicScenario } from "../../support/mockControl";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

const selectionScrollRoot = (page: Page) =>
  page.getByTestId("machine-recipe-selection").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
  await setTopicScenario(page, "machineRecipesDefault");
});

test("選択済機械は大型パネルでヘッダ＋レシピ分スロット＋ゴーストを出し、タブを持たない", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-tab-switch")).toHaveCount(0);
  await expect(page.getByTestId("machine-selected-recipe")).toBeVisible();
  await expect(page.getByTestId("machine-selected-recipe-time")).toContainText("10");
  // 入力は素材数(1)・出力は生産物数(1)だけ描く（機械は入2/出1）
  // Draw only recipe-count slots: 1 input, 1 output (the machine itself has 2/1)
  await expect(page.getByTestId("machine-input-slots").locator("> div")).toHaveCount(1);
  await expect(page.getByTestId("machine-output-slots").locator("> div")).toHaveCount(1);
  // 空の出力スロットはゴースト、実物のある入力スロットはゴースト無し
  // The empty output slot is a ghost; the occupied input slot is not
  await expect(page.getByTestId("machine-output-slots").locator('[data-ghost="true"]')).toHaveCount(1);
  await expect(page.getByTestId("machine-input-slots").locator('[data-ghost="true"]')).toHaveCount(0);
  await expect(page.getByTestId("machine-fluid-slots").locator('[data-ghost="true"]')).toHaveCount(1);
  await expect(page.getByTestId("machine-power-rate")).toBeVisible();
  await expect(page.getByTestId("machine-state-label")).toBeVisible();
});

test("ヘッダクリックでレシピ選択モードへ戻り、行クリックでインベントリモードへ戻る", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  const selection = page.getByTestId("machine-recipe-selection");
  await expect(selection).toBeVisible();
  await expect(page.getByTestId("machine-inventory-body")).toHaveCount(0);
  await expect(selection.locator('[data-testid^="machine-recipe-"][data-testid$="-name"]')).toHaveCount(3);
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");
  await expect(page.getByTestId(`${selectedRecipeTestId}-row-duration`)).toContainText("10");

  // 右クリックは解除を送らない（選択が残る）
  // Right-click never clears (the selection stays)
  await page.getByTestId(selectedRecipeTestId).click({ button: "right" });
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");

  await page.getByTestId(firstRecipeTestId).click();
  await expect(page.getByTestId("machine-inventory-body")).toBeVisible();
  await expect(page.getByTestId("machine-selected-recipe-time")).toContainText("5");
});

test("レシピ未選択の機械はレシピ選択モードで開く", async ({ page }) => {
  await setBlock(page, "gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
  await expect(page.getByTestId("machine-inventory-body")).toHaveCount(0);
});

test("レシピ無しブロックは小型パネルのまま", async ({ page }) => {
  await setBlock(page, "generator");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByTestId("block-inventory")).not.toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-recipe-selection")).toHaveCount(0);
});

test("レシピが溢れてもパネル高は変わらず、選択リストだけがスクロールする", async ({ page }) => {
  // 溢れ用fixtureは電気機械のGUIDへ12件足すため、選択済の電気機械をヘッダから選択モードへ戻して見る
  // The overflow fixture adds twelve recipes to the electric machine's GUID, so reopen selection mode from its header
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
  const viewport = selectionScrollRoot(page).locator(".mantine-ScrollArea-viewport");
  const bar = selectionScrollRoot(page).locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]');

  // 既定fixtureは溢れないため、バーは出ずスクロール量も0
  // The default fixture does not overflow, so no bar appears and there is nothing to scroll
  const settledHeight = (await page.getByTestId("block-inventory").boundingBox())!.height;
  await expect(bar).toBeHidden();
  expect(await viewport.evaluate((element) => element.scrollHeight - element.clientHeight)).toBe(0);

  // 溢れる件数でもパネルは伸びない。伸びるならスクロールが始まらない（前例: recipeListScroll.spec）
  // An overflowing count must not grow the panel; if it grows, scrolling never starts (precedent: recipeListScroll.spec)
  await setTopicScenario(page, "machineRecipesOverflow");
  await expect(bar).toBeVisible();
  expect((await page.getByTestId("block-inventory").boundingBox())!.height).toBeCloseTo(settledHeight, 1);
  expect(await viewport.evaluate((element) => element.scrollHeight - element.clientHeight)).toBeGreaterThan(0);
});
