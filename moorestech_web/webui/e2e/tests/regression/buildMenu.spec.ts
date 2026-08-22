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
  // 必要素材が詳細に出ることを固定
  // Pins the required-items block in the detail
  await expect(page.getByTestId("build-menu-detail")).toContainText("必要素材");

  // 検索欄退避でもsticky維持
  // Stays sticky after moving to search
  await page.getByTestId("build-menu-search").hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("木のチェスト");
});

test("エントリの無いカテゴリはサイドバーに出ない", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // 「建材」はエントリ皆無のため除外
  // Skips empty category
  await expect(page.getByTestId("build-menu-sidebar").locator("button")).toHaveCount(10);
});

test("閉じて開き直すとタブ・検索・スクロール・詳細stickyが復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // タブ+sticky+スクロール構築
  // Build state, then close
  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`).hover();
  // 合成scrollイベントは使わない
  // No synthetic scroll event
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

test("残り設置数は1セット複数個のエントリだけに表示される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // beltはN=3。残数を常時表示
  // beltConveyor has placementsPerCost=3 with empty requiredItems; remaining placements must show regardless of required items
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.beltConveyor}`).hover();
  await expect(page.getByTestId("build-menu-remaining-placements")).toBeVisible();
  await expect(page.getByTestId("build-menu-remaining-placements")).toContainText("2");

  // woodChestはN=1のため残数を表示しない
  // woodChest has placementsPerCost=1, so the remaining-placements row must not show
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`).hover();
  await expect(page.getByTestId("build-menu-remaining-placements")).toBeHidden();
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
