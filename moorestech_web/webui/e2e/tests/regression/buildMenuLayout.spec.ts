// §8.11の寸法契約（中央寄せ・固定高カテゴリ・全カテゴリ収容・8列グリッド）を目視QAで確定した値のまま守らせる
// Locks the §8.11 dimension contract (centering, fixed-height categories, full category fit, 8-column grid) to the values settled by visual QA

import { test, expect } from "@playwright/test";
import { setUiState } from "../../support/mockControl";

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

  // offsetHeightはborder込みのボーダーボックス高で、border-box指定の高さトークンと同じ量。stageの拡大縮小も受けない
  // offsetHeight is the border-box height including borders, exactly what the height token sets under border-box sizing, and it ignores the stage scale
  // 整数丸めと小数トークン(2.3rem=36.8px等)を吸収するため±1pxを許容する
  // Allow +/-1px to absorb integer rounding and fractional tokens such as 2.3rem = 36.8px
  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  const count = await buttons.count();
  for (let i = 0; i < count; i += 1) {
    const height = await buttons.nth(i).evaluate((el: HTMLElement) => el.offsetHeight);
    expect(Math.abs(height - expected)).toBeLessThanOrEqual(1);
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

  // 最後のボタン下端を3列コンテナのコンテンツボックス下端と直接比べる。scrollHeight比較はpadding-bottomに吸われて実残余約19pxを検出できない
  // Compare the last button's bottom against the three-column container's content-box bottom; a scrollHeight comparison is absorbed by padding-bottom and cannot see the real ~19px of slack
  const fit = await page.getByTestId("build-menu-columns").evaluate((columns: HTMLElement) => {
    const buttonList = columns.querySelectorAll('[data-testid="build-menu-sidebar"] button');
    const columnsRect = columns.getBoundingClientRect();
    // stageのCSS transform倍率。paddingはレイアウトpxなので同じ倍率で表示pxへ揃える
    // The stage's CSS transform scale; padding is a layout px value, so scale it into displayed px the same way
    const scale = columnsRect.height / columns.offsetHeight;
    const paddingBottom = parseFloat(getComputedStyle(columns).paddingBottom) * scale;
    const lastBottom = buttonList[buttonList.length - 1].getBoundingClientRect().bottom;
    return { lastBottom, contentBottom: columnsRect.bottom - paddingBottom };
  });
  expect(fit.lastBottom).toBeLessThanOrEqual(fit.contentBottom);
});

test("グリッドは8列を保ち中央列に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 列数はSlotGridへ渡すprop値そのもので、独自gridへの差し替え(§8.11違反)だけを検出する
  // The column count is exactly the prop handed to SlotGrid, so it only catches a swap to an ad-hoc grid (a §8.11 violation)
  const grid = page.locator('[data-testid^="build-menu-grid-"]').first();
  const columnCount = await grid.evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(columnCount).toBe(8);

  // 寸法のガードはこちら。8列の実幅が中央列(=検索入力の幅)を超えれば、パネル幅・サイドバー幅・詳細幅のどれが動いても検出できる
  // This is the dimensional guard: once the eight columns outgrow the center column (the search input's width), drift in the panel, sidebar, or detail width surfaces
  const gridBox = await grid.boundingBox();
  const searchBox = await page.getByTestId("build-menu-search").boundingBox();
  if (!gridBox || !searchBox) throw new Error("bounding box unavailable");
  expect(gridBox.width).toBeLessThanOrEqual(searchBox.width);
});
