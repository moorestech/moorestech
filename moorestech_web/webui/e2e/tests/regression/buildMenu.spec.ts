import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";

const logisticsCategoryGuid = "51000000-0000-4000-8000-000000000001";
const transportCategoryGuid = "51000000-0000-4000-8000-000000000002";
const blueprintCategoryGuid = "51000000-0000-4000-8000-000000000003";
const chestSubCategoryGuid = "52000000-0000-4000-8000-000000000001";
const railSubCategoryGuid = "52000000-0000-4000-8000-000000000003";
const woodChestBlockGuid = "53000000-0000-4000-8000-000000000001";
const railBlockGuid = "53000000-0000-4000-8000-000000000004";
const cargoCarGuid = "56000000-0000-4000-8000-000000000001";
const wireConnectToolGuid = "55000000-0000-4000-8000-000000000001";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("ui_stateでビルドメニューを開閉し既定カテゴリのエントリを表示する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-panel")).toBeVisible();
  await expect(page.getByTestId(`build-menu-category-${logisticsCategoryGuid}`)).toContainText("物流");
  await expect(page.getByTestId(`build-menu-category-${transportCategoryGuid}`)).toContainText("輸送");
  await expect(page.getByTestId(`build-menu-section-${logisticsCategoryGuid}-${chestSubCategoryGuid}`)).toContainText("チェスト");
  await expect(page.getByTestId(`build-menu-entry-block-${woodChestBlockGuid}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-trainCar-${cargoCarGuid}`)).toBeHidden();

  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});

test("エントリ選択とBP右クリック削除のアクション契約", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId(`build-menu-entry-block-${woodChestBlockGuid}`).click();
  await expect.poll(() => payloadsOf(page, "build_menu.select")).toContainEqual({
    entryType: "block",
    entryKey: woodChestBlockGuid,
  });

  await page.getByTestId(`build-menu-category-${blueprintCategoryGuid}`).click();
  await expect(page.getByTestId("build-menu-entry-blueprintCopy-")).toContainText("ブループリントコピー");
  await page.getByTestId("build-menu-entry-blueprint-starter-base").click({ button: "right" });
  await expect.poll(() => payloadsOf(page, "blueprint.delete")).toContainEqual({ name: "starter-base" });
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

  await expect(page.getByTestId(`build-menu-section-${logisticsCategoryGuid}-${chestSubCategoryGuid}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-block-${railBlockGuid}`)).toBeHidden();

  await page.getByTestId(`build-menu-category-${transportCategoryGuid}`).click();
  await expect(page.getByTestId(`build-menu-entry-block-${railBlockGuid}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-entry-block-${woodChestBlockGuid}`)).toBeHidden();

  // connectToolはlabel無配信でもGuid導出キーの辞書名で表示される
  // connectTool is presented with its Guid-derived dictionary name even though no label is delivered
  await page.getByTestId(`build-menu-entry-connectTool-${wireConnectToolGuid}`).hover();
  await expect(page.getByTestId("build-menu-preview")).toContainText("電線接続ツール");
});

test("横断検索は複合見出しで区切りサイドバーを無効化する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await expect(page.getByTestId(`build-menu-section-${logisticsCategoryGuid}-${chestSubCategoryGuid}`)).toContainText("物流 / チェスト");
  await expect(page.getByTestId(`build-menu-section-${transportCategoryGuid}-${railSubCategoryGuid}`)).toContainText("輸送 / 鉄道");
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");

  await page.getByTestId("build-menu-search").fill("");
  await expect(page.getByTestId("build-menu-sidebar")).not.toHaveAttribute("data-disabled", "true");
  await expect(page.getByTestId(`build-menu-section-${logisticsCategoryGuid}-${chestSubCategoryGuid}`)).toBeVisible();
});

test("検索0件は該当なし表示", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("存在しないブロック");
  await expect(page.getByTestId("build-menu-panel")).toContainText("該当なし");
});

test("ホバーでプレビューが更新される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId(`build-menu-entry-block-${woodChestBlockGuid}`).hover();
  await expect(page.getByTestId("build-menu-preview")).toContainText("木のチェスト");
});

test("エントリの無いカテゴリはサイドバーに出ない", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // fixturesは4カテゴリ定義だが「建材」はエントリ皆無のため、エントリを持つ3カテゴリのみが並ぶ
  // fixtures define 4 categories but "建材" has no entries, so only the 3 with entries render
  await expect(page.getByTestId("build-menu-sidebar").locator("button")).toHaveCount(3);
});
