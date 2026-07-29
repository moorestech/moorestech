import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setUiState } from "../support/mockControl";

// 装備HUDは data-testid="equipment-slots" で特定し、直下の各 div が1枠になる
// The equipment HUD is identified via data-testid="equipment-slots"; each direct child div is one slot
const equipmentSlots = (page: import("@playwright/test").Page) =>
  page.getByTestId("equipment-slots").locator("> div");

// 画面状態を変えるテストがあるため、既定状態へ戻して他 spec へ漏らさない
// Some tests change the screen state; reset to defaults so it never leaks into other specs
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("小さいホイール入力を累積し閾値を越えた時だけ切り替える", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("equipment-slots")).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  await page.mouse.wheel(0, 40);
  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBe(before);
  await page.mouse.wheel(0, 70);
  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBe(before + 1);
});

test("ホイールは末尾スロットの次に素手(-1)を挟む", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("equipment-slots")).toBeVisible();

  // fixture は素手(-1)開始。3枠を1周すると -1 へ戻り、どの枠も選択されない
  // The fixture starts at bare hands (-1); one lap over three slots returns to -1 with no slot selected
  for (let step = 0; step < 3; step++) await page.mouse.wheel(0, 100);
  await expect(equipmentSlots(page).nth(2)).toHaveAttribute("data-selected", "true");

  await page.mouse.wheel(0, 100);
  await expect.poll(() => payloadsOf(page, "inventory.select_equipment")).toContainEqual({ index: -1 });
  await expect(equipmentSlots(page).nth(2)).not.toHaveAttribute("data-selected", "true");
});

test("空枠のクリックでもその枠が選択される", async ({ page }) => {
  await page.goto("/");
  // fixture の 3枠目は空。空でも選択対象になる
  // The fixture's third slot is empty; empty slots are still selectable
  await equipmentSlots(page).nth(2).click();

  await expect.poll(() => payloadsOf(page, "inventory.select_equipment")).toContainEqual({ index: 2 });
  await expect(equipmentSlots(page).nth(2)).toHaveAttribute("data-selected", "true");
});
