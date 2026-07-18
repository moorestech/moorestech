import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setUiState } from "../support/mockControl";

// ホットバーグリッドは data-testid="hotbar-grid" で特定する
// The hotbar grid is identified via data-testid="hotbar-grid"
const hotbarSlots = (page: import("@playwright/test").Page) =>
  page.getByTestId("hotbar-grid").locator("> div > div");

// 画面状態を変えるテストがあるため、既定状態へ戻して他 spec へ漏らさない
// Some tests change the screen state; reset to defaults so it never leaks into other specs
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("小さいホイール入力を累積し閾値を越えた時だけ切り替える", async ({ page }) => {
  await page.goto("/");
  const hotbar = page.getByTestId("hotbar-grid");
  await hotbar.hover();
  const before = (await payloadsOf(page, "inventory.select_hotbar")).length;

  await page.mouse.wheel(0, 40);
  await expect.poll(async () => (await payloadsOf(page, "inventory.select_hotbar")).length).toBe(before);
  await page.mouse.wheel(0, 70);
  await expect.poll(async () => (await payloadsOf(page, "inventory.select_hotbar")).length).toBe(before + 1);
});

test("ホットバー slot 0 が初期選択（data-selected）", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
  // fixture の selectedHotbar:0 により slot 0 が選択状態
  // The fixture's selectedHotbar:0 marks slot 0 as selected
  await expect(hotbarSlots(page).nth(0)).toHaveAttribute("data-selected", "true");
  await expect(hotbarSlots(page).nth(1)).not.toHaveAttribute("data-selected", "true");
});

test('"2" キーで select_hotbar{index:1} を送り、選択が slot 1 へ移る', async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
  await expect(hotbarSlots(page).nth(0)).toHaveAttribute("data-selected", "true");

  await page.keyboard.press("2");

  // host が select_hotbar を受理したことを action 記録で確認
  // Confirm the host received select_hotbar via the action log
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "inventory.select_hotbar");
      return payloads[0] as { index?: number } | undefined;
    })
    .toEqual({ index: 1 });

  // mock が selectedHotbar=1 で inventory event を push → slot 1 が選択、slot 0 は非選択
  // The mock pushes an inventory event with selectedHotbar=1 → slot 1 selected, slot 0 not
  await expect(hotbarSlots(page).nth(1)).toHaveAttribute("data-selected", "true");
  await expect(hotbarSlots(page).nth(0)).not.toHaveAttribute("data-selected", "true");
});

test("GameScreen でもホットバーは表示されキー選択が効く（uGUI HUD準拠）", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();

  await setUiState(page, "GameScreen");
  // インベントリ画面は閉じるが、ホットバーHUDは残る
  // The inventory screen closes while the hotbar HUD stays
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeHidden();
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  // GameScreen 中も 1-9 キーでの持ち替えが機能する
  // Hotbar switching via keys 1-9 keeps working during GameScreen
  await page.keyboard.press("3");
  await expect(hotbarSlots(page).nth(2)).toHaveAttribute("data-selected", "true");
});
