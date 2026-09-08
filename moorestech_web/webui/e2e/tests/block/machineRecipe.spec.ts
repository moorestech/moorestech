import { test, expect } from "@playwright/test";
import { setBlock, setTopicScenario } from "../../support/mockControl";
import { scrollAreaRootOf, scrollAreaViewport, expectScrollsOnlyWhenOverflowing } from "../../support/layoutAssertions";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

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

test("レシピが溢れても機械セクションの高さは変わらず、選択リストだけがスクロールする", async ({ page }) => {
  // 溢れ用fixtureは電気機械へ入る
  // The overflow fixture targets the electric machine
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();

  // 高さ不変はパネルでなく機械セクションで見る。パネル高は.panelLargeのCSS固定値で件数に依らず動かないため
  // Watch the machine section, not the panel: the panel's height is a CSS constant in .panelLarge and cannot move
  await expectScrollsOnlyWhenOverflowing(
    scrollAreaRootOf(page, "machine-recipe-selection"),
    page.getByTestId("machine-section"),
    () => setTopicScenario(page, "machineRecipesOverflow"),
  );
});

test("フッタは選択モードとインベントリモードで同じ高さに出る", async ({ page }) => {
  // フッタが両モードで下端に揃うことがこの変更の中核ゴール（ADR 0010）
  // Aligning the footer at the bottom in both modes is this change's core goal (ADR 0010)
  await setBlock(page, "machine");
  await page.goto("/");
  const footer = page.getByTestId("machine-state-label");
  await expect(footer).toBeVisible();
  const inventoryModeTop = (await footer.boundingBox())!.y;

  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
  const selectionModeTop = (await footer.boundingBox())!.y;

  expect(selectionModeTop).toBeCloseTo(inventoryModeTop, 1);
});

// 控えめ設定の実測は3.9行分。下回れば密度上書きが消えた証拠、超えれば詰めすぎで非目標側へ振れた証拠
// The modest setting measures 3.9 rows: below means the density overrides vanished, above means it was over-tightened
const minimumVisibleRows = 3.8;
const maximumVisibleRows = 4.5;

test("行密度は控えめ設定どおりで、本文に3.8〜4.5行分が入る", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  const list = page.getByTestId("machine-recipe-selection");
  await expect(list).toBeVisible();

  // ストライドは1行の実寸＋リストの行間から組む。特定2行が隣接する並びに依存させない
  // Build the stride from one row's measured height plus the list gap, never from two specific rows being adjacent
  const rowHeight = (await page.getByTestId(firstRecipeTestId).boundingBox())!.height;
  const rowGap = await list.evaluate((element) => Number.parseFloat(getComputedStyle(element).rowGap));
  const rowStride = rowHeight + rowGap;
  const viewport = scrollAreaViewport(scrollAreaRootOf(page, "machine-recipe-selection"));
  const clientHeight = await viewport.evaluate((element) => element.clientHeight);

  expect(rowGap).toBeGreaterThan(0);
  expect(clientHeight / rowStride).toBeGreaterThan(minimumVisibleRows);
  expect(clientHeight / rowStride).toBeLessThan(maximumVisibleRows);
});
