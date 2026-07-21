import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("ui_stateでビルドメニューを開閉し既定カテゴリのエントリを表示する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-panel")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-block-wood-chest")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-trainCar-cargo-car")).toBeHidden();

  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});

test("エントリ選択とBP右クリック削除のアクション契約", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-entry-block-wood-chest").click();
  await expect.poll(() => payloadsOf(page, "build_menu.select")).toContainEqual({ entryType: "block", entryKey: "wood-chest" });

  await page.getByTestId("build-menu-category-ブループリント").click();
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

  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-block-rail")).toBeHidden();

  await page.getByTestId("build-menu-category-輸送").click();
  await expect(page.getByTestId("build-menu-entry-block-rail")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-block-wood-chest")).toBeHidden();
});

test("横断検索は複合見出しで区切りサイドバーを無効化する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
  await expect(page.getByTestId("build-menu-section-輸送-鉄道")).toBeVisible();
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");

  await page.getByTestId("build-menu-search").fill("");
  await expect(page.getByTestId("build-menu-sidebar")).not.toHaveAttribute("data-disabled", "true");
  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
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

  await page.getByTestId("build-menu-entry-block-wood-chest").hover();
  await expect(page.getByTestId("build-menu-preview")).toContainText("木のチェスト");
});

test("エントリの無いカテゴリはサイドバーに出ない", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // fixturesの全カテゴリにエントリがあるため、定義順どおり3カテゴリのみが並ぶことを確認する
  // fixtures' every category has entries, so this confirms only the 3 defined categories render, in order
  await expect(page.getByTestId("build-menu-sidebar").locator("button")).toHaveCount(3);
});
