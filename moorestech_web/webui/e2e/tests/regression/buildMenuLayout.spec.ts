// §8.11の寸法契約（中央寄せ・固定高カテゴリ・全カテゴリ収容・8列グリッド）を目視QAで確定した値のまま守らせる
// Locks the §8.11 dimension contract (centering, fixed-height categories, full category fit, 8-column grid) to the values settled by visual QA

import { test, expect } from "@playwright/test";
import { expectNoVerticalOverflow } from "../../support/layoutAssertions";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { buildMenuCategoryIds } from "../../mock-host/fixtures";

// 視覚寸法トークンを実px化する。remは:rootのfont-sizeで解く
// Resolves a visual dimension token into pixels, converting rem against the :root font size
async function tokenPixels(page: import("@playwright/test").Page, name: string) {
  return page.evaluate((tokenName) => {
    const rootStyle = getComputedStyle(document.documentElement);
    const raw = rootStyle.getPropertyValue(tokenName).trim();
    return raw.endsWith("rem") ? parseFloat(raw) * parseFloat(rootStyle.fontSize) : parseFloat(raw);
  }, name);
}

// mock-hostのlocaleは接続横断の共有状態なので、英語へ切り替えた回も既定へ戻してから抜ける
// The mock host's locale is shared across connections, so every run returns it to the default before leaving
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "japanese");
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

test("パネルは上部安全帯の直下にバンド高いっぱいで立つ", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // R2「縦位置は現状維持」の契約。期待値はトークンから解き、stage拡大率はパネル実幅÷幅トークンで求める
  // Contract for R2 "vertical placement unchanged": expectations come from tokens, and the stage scale is the panel's real width over the width token
  const [panelWidth, upperSafeArea, contentHeight] = await Promise.all([
    tokenPixels(page, "--build-menu-panel-width"),
    tokenPixels(page, "--menu-upper-safe-area"),
    tokenPixels(page, "--menu-content-height"),
  ]);

  // stageはcenter originで拡縮するため絶対yではなくstage枠からの相対で測る。額縁は高さ注入の消失を映す
  // The stage scales from its center, so measure relative to the stage box rather than absolute y; the frame height reflects a lost height injection
  const geometry = await page.getByTestId("build-menu-panel").evaluate((panel: HTMLElement) => {
    const stage = (panel.offsetParent as HTMLElement).offsetParent as HTMLElement;
    const panelRect = panel.getBoundingClientRect();
    const frameRect = panel.querySelector("[data-variant]")!.getBoundingClientRect();
    return {
      top: panelRect.top - stage.getBoundingClientRect().top,
      height: panelRect.height,
      width: panelRect.width,
      frameHeight: frameRect.height,
    };
  });

  // 整数丸めと拡大率の割り戻しを吸収するため±1pxを許容する
  // Allow +/-1px to absorb integer rounding and the scale division
  const scale = geometry.width / panelWidth;
  expect(Math.abs(geometry.top / scale - upperSafeArea)).toBeLessThanOrEqual(1);
  expect(Math.abs(geometry.height / scale - contentHeight)).toBeLessThanOrEqual(1);
  expect(Math.abs(geometry.frameHeight - geometry.height)).toBeLessThanOrEqual(1);
});

test("カテゴリボタンは全ボタン同一の固定高", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 期待値はトークンから解く。トークン再調整で壊れず、伸縮実装(パネル高÷カテゴリ数)なら一致しない
  // Derive the expectation from the token so retuning it never breaks the test, while a stretching implementation (panel height / category count) still fails
  const expected = await tokenPixels(page, "--build-menu-category-height");

  // offsetHeightはborder込みのボーダーボックス高で、border-box指定の高さトークンと同じ量。stageの拡大縮小も受けない
  // offsetHeight is the border-box height including borders, exactly what the height token sets under border-box sizing, and it ignores the stage scale
  // 整数丸めと小数トークン(2.3rem=36.8px等)を吸収するため±1pxを許容する
  // Allow +/-1px to absorb integer rounding and fractional tokens such as 2.3rem = 36.8px
  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  // 0件ならループが素通りして無条件greenになるため、fixturesの10カテゴリを先に固定する
  // A zero count would skip the loop and pass unconditionally, so pin the fixtures' ten categories first
  const count = await buttons.count();
  expect(count).toBe(10);
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
  await expectNoVerticalOverflow(page.getByTestId("build-menu-sidebar").locator("button"));

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

test("英語ロケールでもカテゴリ名は1行のまま固定高に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await setTopicScenario(page, "english");
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("data-locale", "english");

  // 実マスタ最長の英訳が可視カテゴリに載っていることを先に固定する。載らなければ折返し検査が素通りする
  // Pin that the real master's longest English name is on a visible category first; otherwise the wrap check passes vacuously
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.buildingMaterial}`))
    .toHaveText("Building Materials");

  // 折返し判定はwebフォントの字幅に依存するため、フォント確定後に測る
  // Wrapping depends on the web font's advance widths, so measure only after the fonts settle
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await expectNoVerticalOverflow(page.getByTestId("build-menu-sidebar").locator("button"));
});

test("グリッドは8列を保ち中央列に収まる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 列数はSlotGridへ渡すprop値そのもので、独自gridへの差し替え(§8.11違反)だけを検出する
  // The column count is exactly the prop handed to SlotGrid, so it only catches a swap to an ad-hoc grid (a §8.11 violation)
  const grid = page.locator('[data-testid^="build-menu-grid-"]').first();
  const columnCount = await grid.evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(columnCount).toBe(8);

  // 寸法のガードはこちら。8列の実幅がスクロールバー予約を除いた収容幅を超えれば、パネル幅・サイドバー幅・詳細幅のどれが動いても検出できる
  // This is the dimensional guard: once the eight columns outgrow the space left by the scrollbar reserve, drift in the panel, sidebar, or detail width surfaces
  const fit = await page.getByTestId("build-menu-sections").evaluate((area: HTMLElement) => {
    const style = getComputedStyle(area);
    const reserve = parseFloat(style.paddingRight);
    return {
      reserve,
      contentWidth: area.clientWidth - parseFloat(style.paddingLeft) - reserve,
      gridWidth: area.querySelector<HTMLElement>('[data-testid^="build-menu-grid-"]')!.offsetWidth,
    };
  });
  // 予約が消えるとオーバーレイスクロールバーが8列目へ重なる。収容幅の比較だけでは通ってしまうため予約自体も固定する
  // Losing the reserve lets the overlay scrollbar sit on the eighth column, and the width comparison alone would still pass, so pin the reserve itself
  expect(fit.reserve).toBeGreaterThan(0);
  expect(fit.gridWidth).toBeLessThanOrEqual(fit.contentWidth);
});
