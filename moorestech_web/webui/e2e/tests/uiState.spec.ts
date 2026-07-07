import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setBlock, setUiState } from "../support/mockControl";

// 各テスト後に既定状態へ戻し、他 spec へ画面状態を漏らさない
// Reset to defaults after each test so screen state never leaks into other specs
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setBlock(page, "closed");
});

test("既定(PlayerInventory)でインベントリ画面が表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
});

test("GameScreen でパネルが消え、PlayerInventory への event で再表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();

  await setUiState(page, "GameScreen");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeHidden();

  await setUiState(page, "PlayerInventory");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
});

test("SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // RECIPE_LOCATOR: recipe.spec.ts と同一セレクタで RecipeViewer の非表示を検証する
  // RECIPE_LOCATOR: assert the RecipeViewer is hidden using the same selector as recipe.spec.ts
  await expect(page.getByRole("heading", { name: "Items" })).toBeHidden();
});

test("ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();

  await page.getByTestId("block-inventory-close").click();

  // action 送信契約を action 記録で検証する
  // Verify the send contract via the action log
  await expect
    .poll(async () => {
      return (await payloadsOf(page, "ui_state.request")).some((payload) => (payload as { state?: string }).state === "GameScreen");
    })
    .toBe(true);

  // mock が ui_state/block event を返し、インベントリ画面とブロックパネルが閉じる
  // The mock pushes ui_state/block events back, closing the inventory screen and block panel
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeHidden();
  await expect(page.getByTestId("block-inventory")).toBeHidden();
});
