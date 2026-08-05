import { test, expect } from "@playwright/test";
import { setUiState } from "../../support/mockControl";

// §8.11の寸法契約（中央寄せ・固定高カテゴリ・全カテゴリ収容）を目視QAで確定した値のまま守らせる
// Locks the §8.11 dimension contract (centering, fixed-height categories, full category fit) to the values settled by visual QA
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("パネルは画面水平中央に表示される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const box = await page.getByTestId("build-menu-panel").boundingBox();
  const viewport = page.viewportSize();
  if (!box || !viewport) throw new Error("bounding box unavailable");
  expect(Math.abs(box.x + box.width / 2 - viewport.width / 2)).toBeLessThanOrEqual(1);
});

test("カテゴリボタンは全ボタン同一の固定高", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 期待値はトークンから解く。トークン再調整で壊れず、伸縮実装(パネル高÷カテゴリ数)なら一致しない
  // Derive the expectation from the token so retuning it never breaks the test, while a stretching implementation (panel height / category count) still fails
  const expected = await page.evaluate(() => {
    const rootStyle = getComputedStyle(document.documentElement);
    const raw = rootStyle.getPropertyValue("--build-menu-category-height").trim();
    return raw.endsWith("rem") ? parseFloat(raw) * parseFloat(rootStyle.fontSize) : parseFloat(raw);
  });

  // clientHeightはstageの拡大縮小を受けないレイアウト値のため、トークンpxと直接比較できる
  // clientHeight is a layout value untouched by the stage scale, so it compares directly against the token px
  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  const count = await buttons.count();
  for (let i = 0; i < count; i += 1) {
    expect(await buttons.nth(i).evaluate((el) => el.clientHeight)).toBe(expected);
  }
});

test("カテゴリサイドバーは全カテゴリとラベルを固定高のまま収める", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // ラベルがボタン高を超えると折り返し分がscrollHeightに出る。§8.11はサイドバー独自スクロールを持たないためはみ出しは即欠陥
  // A label taller than its button shows the wrapped overflow in scrollHeight; §8.11 gives the sidebar no scroller, so any overflow is a defect
  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  const count = await buttons.count();
  for (let i = 0; i < count; i += 1) {
    const fit = await buttons.nth(i).evaluate((el) => ({ client: el.clientHeight, scroll: el.scrollHeight }));
    expect(fit.scroll).toBeLessThanOrEqual(fit.client);
  }

  // 縦積みが利用可能高を超えると3列の親(固定高)の行が伸び、差分がscrollHeightに出る
  // If the stack exceeds the available height, the fixed-height three-column parent's row grows and the excess shows in scrollHeight
  const columns = await page.getByTestId("build-menu-detail").evaluate((el) => {
    const parent = el.parentElement as HTMLElement;
    return { client: parent.clientHeight, scroll: parent.scrollHeight };
  });
  expect(columns.scroll).toBeLessThanOrEqual(columns.client);
});

// 8列SlotGridは中央列の実幅に依存するため、パネル幅・サイドバー幅・詳細幅の合計が崩れると列数が変わる
// The 8-column SlotGrid depends on the center column's real width, so any drift in panel/sidebar/detail widths changes the column count
test("グリッドは8列を保ち中央列に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const grid = page.getByTestId("build-menu-panel").locator('[data-testid^="build-menu-section"] > div').nth(1);
  const columnCount = await grid.evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(columnCount).toBe(8);

  const gridBox = await grid.boundingBox();
  const searchBox = await page.getByTestId("build-menu-search").boundingBox();
  if (!gridBox || !searchBox) throw new Error("bounding box unavailable");
  expect(gridBox.width).toBeLessThanOrEqual(searchBox.width);
});
