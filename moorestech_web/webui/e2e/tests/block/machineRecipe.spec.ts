import { test, expect, type Page } from "@playwright/test";
import { setBlock, setTopicScenario } from "../../support/mockControl";
import { scrollAreaRootOf, scrollAreaViewport, expectScrollsOnlyWhenOverflowing } from "../../support/layoutAssertions";
import { FLUID_ICON_PREFIX } from "../../../src/bridge/transport/httpEndpoints";
import { OIL_FLUID_GUID } from "../../mock-host/fixtures/contentLocalizationFixtures";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const fluidRecipeTestId = "machine-recipe-cccccccc-cccc-4ccc-8ccc-cccccccccccc";

// mock-hostは液体アイコンを404にするため、原寸描画の崩れ（ADR 0054の起点）を再現するには大きな画像を実際に読ませる必要がある
// The mock host 404s fluid icons, so reproducing the natural-size overflow (ADR 0054's origin) needs a large image to actually load
const largeFluidIconSvg = '<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512"><rect width="512" height="512" fill="#2A6FE0"/></svg>';

// 権威定数を経由してルートを張る。改名すればここも追従し、mock-hostの404へ無言で外れない
// Route through the authoritative constant so a rename here tracks the app instead of silently falling through to the mock host's 404
async function serveLargeFluidIcons(page: Page) {
  const routed = { hit: false };
  await page.route(`**${FLUID_ICON_PREFIX}*.png`, (route) => {
    routed.hit = true;
    return route.fulfill({ contentType: "image/svg+xml", body: largeFluidIconSvg });
  });
  return routed;
}

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

test("レシピが溢れても選択リストの視口高は変わらず、そこだけがスクロールする", async ({ page }) => {
  // 溢れ用fixtureは電気機械へ入る
  // The overflow fixture targets the electric machine
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();

  await expectScrollsOnlyWhenOverflowing(
    scrollAreaRootOf(page, "machine-recipe-selection"),
    () => setTopicScenario(page, "machineRecipesOverflow"),
  );
});

test("フッタは選択モードとインベントリモードで同じ高さに出る", async ({ page }) => {
  // フッタが両モードで下端に揃うのはPRの中核ゴール。番人が無かったので回帰検査として置く（ADR 0010）
  // Aligning the footer in both modes is the PR's core goal and had no guard, so this stands as its regression check (ADR 0010)
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

test("レシピ選択行の液体はアイテムスロットと同寸の枠に収まり、量バッジを出す", async ({ page }) => {
  const routed = await serveLargeFluidIcons(page);
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();

  // アイテム枠は名指しtestIdで取得
  // Take the item slot by its own testId
  const itemSlot = page.getByTestId(`${selectedRecipeTestId}-input-item-0`);
  const fluidSlot = page.getByTestId(`${selectedRecipeTestId}-input-fluid-0`);
  await expect(fluidSlot).toHaveAttribute("data-filled", "true");
  await expect(fluidSlot).toContainText("10");

  const itemBox = (await itemSlot.boundingBox())!;
  const fluidBox = (await fluidSlot.boundingBox())!;
  expect(fluidBox.width).toBeCloseTo(itemBox.width, 0);
  expect(fluidBox.height).toBeCloseTo(itemBox.height, 0);

  // 原寸512pxが枠内に収まる確認
  // Confirm the native 512px image stays inside the frame
  const iconBox = (await fluidSlot.locator("img").boundingBox())!;
  expect(iconBox.width).toBeLessThanOrEqual(fluidBox.width + 0.5);
  expect(iconBox.height).toBeLessThanOrEqual(fluidBox.height + 0.5);
  expect(routed.hit).toBe(true);

  // 4桁側（ccccccccの出力1000）は桁溢れの本命。バッジ矩形はleft/rightで幾何的に固定されるため、
  // 溢れているかは矩形ではなく文字の伸び（scrollWidth）でしか測れない
  // The four-digit side (cccccccc's 1000 output) is where overflow actually bites; the badge box is pinned by left/right,
  // so only the text's own extent (scrollWidth) can tell whether the digits overflow
  const wideBadge = page.getByTestId(`${fluidRecipeTestId}-output-fluid-0-amount`);
  await expect(wideBadge).toHaveText("1,000");
  const badgeOverflow = await wideBadge.evaluate((element) => element.scrollWidth - element.clientWidth);
  expect(badgeOverflow).toBeLessThanOrEqual(0.5);

  // 帯の左端は枠の左端。D2で選んだleft/text-alignが戻ると帯が縮み、この比較で落ちる
  // The band starts at the frame's left edge; reverting D2's left/text-align shrinks it and fails here
  const wideFluidBox = (await page.getByTestId(`${fluidRecipeTestId}-output-fluid-0`).boundingBox())!;
  const wideBadgeBox = (await wideBadge.boundingBox())!;
  expect(wideBadgeBox.x).toBeGreaterThanOrEqual(wideFluidBox.x - 0.5);
  expect(wideBadgeBox.x).toBeLessThanOrEqual(wideFluidBox.x + 0.5);
});

test("液体アイコンが404なら辞書の液体名を枠内に収めて出す", async ({ page }) => {
  // ここだけアイコンをstubしない。mock-hostの404がそのままフォールバック経路の実測になる
  // This is the one test that leaves the icon unstubbed, so the mock host's 404 exercises the fallback path for real
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();

  const fluidSlot = page.getByTestId(`${fluidRecipeTestId}-output-fluid-0`);
  await expect(fluidSlot.locator("img")).toHaveCount(0);
  await expect(fluidSlot).toHaveAttribute("data-filled", "true");
  // 36文字GUIDではなく辞書名を出す
  // Show the dictionary name, never the 36-char GUID
  await expect(fluidSlot).not.toContainText(OIL_FLUID_GUID);
  const nameLabel = fluidSlot.getByText("Oil", { exact: true });
  await expect(nameLabel).toBeVisible();

  // 名前がマスへ収まっていることを実測する。矩形は枠に固定されるので文字の伸びで測る
  // Measure that the name fits the cell; the box is pinned to the frame, so the text's own extent is what tells
  const nameOverflow = await nameLabel.evaluate((element) => ({
    width: element.scrollWidth - element.clientWidth,
    height: element.scrollHeight - element.clientHeight,
  }));
  expect(nameOverflow.width).toBeLessThanOrEqual(0.5);
  expect(nameOverflow.height).toBeLessThanOrEqual(0.5);
  // 辞書名は"Oil"より長くなり得るので、収まりを支える切り詰め規則自体も主張する
  // A dictionary name can be longer than "Oil", so assert the clipping rules that keep any name inside
  await expect(nameLabel).toHaveCSS("white-space", "nowrap");
  await expect(nameLabel).toHaveCSS("text-overflow", "ellipsis");
});

test("液体のみ出力のレシピを選ぶと選択中レシピ表示が液体スロットになる", async ({ page }) => {
  const routed = await serveLargeFluidIcons(page);
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await page.getByTestId(fluidRecipeTestId).click();

  await expect(page.getByTestId("machine-inventory-body")).toBeVisible();
  const headerFluid = page.getByTestId("machine-selected-recipe-fluid");
  await expect(headerFluid).toHaveAttribute("data-filled", "true");
  // 代表液体が出力(石油)であることを確認
  // Confirm the representative fluid is the output (oil)
  await expect(headerFluid.locator("img")).toHaveAttribute("alt", "Oil");
  // バッジ不在はtestIdで主張
  // Assert the badge's absence by its own testId
  await expect(page.getByTestId("machine-selected-recipe-fluid-amount")).toHaveCount(0);
  const headerBox = (await headerFluid.boundingBox())!;
  const iconBox = (await headerFluid.locator("img").boundingBox())!;
  expect(iconBox.width).toBeLessThanOrEqual(headerBox.width + 0.5);
  expect(routed.hit).toBe(true);
  // 入出力タンクがゴースト表示。容量差だけでは束縛先の取り違えを検出できないため量で入力/出力を判別する
  // Input/output tanks render as ghosts; capacity differences alone can't catch a swapped binding, so distinguish input/output by amount
  const fluidGhosts = page.getByTestId("machine-fluid-slots").locator('[data-ghost="true"]');
  await expect(fluidGhosts).toHaveCount(2);
  await expect(fluidGhosts.nth(0)).toContainText("10");
  await expect(fluidGhosts.nth(1)).toContainText("1,000");
});
