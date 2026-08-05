import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";
import {
  buildMenuCategoryIds,
  buildMenuEntryIds,
  buildMenuSubCategoryIds,
} from "../../mock-host/fixtures";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("ui_stateでビルドメニューを開閉し既定カテゴリのエントリを表示する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-panel")).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-trainCar-${buildMenuEntryIds.cargoCar}`)).toBeHidden();

  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});

test("エントリ選択とBP右クリック削除のアクション契約", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`).click();
  await expect.poll(() => payloadsOf(page, "build_menu.select")).toContainEqual({ id: buildMenuEntryIds.woodChest });

  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.blueprint}`).click();
  await page.getByTestId(`build-menu-entry-blueprint-${buildMenuEntryIds.starterBaseBlueprint}`).click({ button: "right" });
  await expect.poll(() => payloadsOf(page, "blueprint.delete")).toContainEqual({ id: buildMenuEntryIds.starterBaseBlueprint });
});

test("閉じるボタンはGameScreen遷移を要求する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");
  await page.getByTestId("build-menu-close").click();

  await expect.poll(() => payloadsOf(page, "ui_state.request")).toContainEqual({ state: "GameScreen" });
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});

test("カテゴリ切替でセクションが入れ替わる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.logistics}-${buildMenuSubCategoryIds.chest}`,
  )).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`)).toBeHidden();

  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`)).toBeHidden();
});

test("横断検索は複合見出しで区切りサイドバーを無効化する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.logistics}-${buildMenuSubCategoryIds.chest}`,
  )).toBeVisible();
  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.transport}-${buildMenuSubCategoryIds.rail}`,
  )).toBeVisible();
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");

  await page.getByTestId("build-menu-search").fill("");
  await expect(page.getByTestId("build-menu-sidebar")).not.toHaveAttribute("data-disabled", "true");
  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.logistics}-${buildMenuSubCategoryIds.chest}`,
  )).toBeVisible();
});

test("検索0件は該当なし表示", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("存在しないブロック");
  await expect(page.getByTestId("build-menu-panel")).toContainText("該当なし");
});

test("詳細サイドバーはホバー後にstickyで残る", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-detail")).toContainText("カーソルを合わせると詳細を表示します");
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`).hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("木のチェスト");
  // 必要素材ブロック（ラベル+3列SlotGrid）が詳細に出ることを固定する
  // Pins that the required-items block (label plus the 3-column SlotGrid) renders in the detail
  await expect(page.getByTestId("build-menu-detail")).toContainText("必要素材");

  // カーソルを検索欄へ退避してもstickyで表示が残る
  // The detail stays sticky after the cursor moves away to the search box
  await page.getByTestId("build-menu-search").hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("木のチェスト");
});

test("エントリの無いカテゴリはサイドバーに出ない", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // fixturesは実マスタ相当の11カテゴリ定義だが「建材」はエントリ皆無のため、エントリを持つ10カテゴリのみが並ぶ
  // fixtures define 11 categories matching the real master but "建材" has no entries, so only the 10 with entries render
  await expect(page.getByTestId("build-menu-sidebar").locator("button")).toHaveCount(10);
});

test("閉じて開き直すとタブ・検索・スクロール・詳細stickyが復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // タブ切替+詳細sticky+スクロールを作ってから閉じる
  // Build up tab selection, sticky detail, and scroll, then close
  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`).hover();
  // scrollイベントの合成発火は使わない。scrollTop代入直後に閉じても保存される実挙動をそのまま検証する
  // No synthetic scroll event: this exercises the real behavior of closing right after assigning scrollTop
  await page
    .getByTestId("build-menu-panel")
    .locator(".mantine-ScrollArea-viewport")
    .evaluate((el) => { el.scrollTop = 40; });
  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();

  await setUiState(page, "BuildMenu");
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`)).toBeVisible();
  await expect(page.getByTestId("build-menu-detail")).toContainText("鉄道レール");
  await expect
    .poll(() =>
      page
        .getByTestId("build-menu-panel")
        .locator(".mantine-ScrollArea-viewport")
        .evaluate((el) => el.scrollTop),
    )
    .toBe(40);
});

test("検索文字列も閉じて開き直すと復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await setUiState(page, "GameScreen");
  await setUiState(page, "BuildMenu");

  await expect(page.getByTestId("build-menu-search")).toHaveValue("鉄");
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");
});
