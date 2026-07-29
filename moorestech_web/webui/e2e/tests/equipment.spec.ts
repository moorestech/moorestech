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
  // 次の起点は最新スナップショットの選択値なので、1段ごとに反映を待ってから次のホイールを送る
  // Each step's origin is the selection in the latest snapshot, so wait for it to land before sending the next wheel
  for (let step = 0; step < 3; step++) {
    await page.mouse.wheel(0, 100);
    await expect(equipmentSlots(page).nth(step)).toHaveAttribute("data-selected", "true");
  }

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

test("装備HUDの上でもホイールで装備が切り替わる", async ({ page }) => {
  await page.goto("/");
  const slots = page.getByTestId("equipment-slots");
  await expect(slots).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  // HUD 自身は実UIだがゲーム操作の場であり、カーソルがここで止まっていても唯一の選択手段を殺してはならない
  // The HUD is real UI yet belongs to the game: parking the cursor on it must not kill the only selection input
  await slots.hover();
  await page.mouse.wheel(0, 100);

  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBe(before + 1);
});

test("ホットバーHUDの上でもホイールで装備が切り替わる", async ({ page }) => {
  await page.goto("/");
  const hotbar = page.getByTestId("hotbar-grid");
  await expect(hotbar).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  await hotbar.hover();
  await page.mouse.wheel(0, 100);

  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBe(before + 1);
});

test("持ち物画面の一覧スクロールでは装備が持ち替わらない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  const grid = page.getByTestId("item-list-grid");
  await expect(grid).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  // 一覧の上へカーソルを置いてからホイールを回し、スクロール操作が装備切替へ二重発火しないことを固定する
  // Park the cursor over the list before wheeling, pinning that the scroll gesture never double-fires as an equipment switch
  await grid.hover();
  for (let step = 0; step < 3; step++) await page.mouse.wheel(0, 100);

  // 後続の素通し1回を同期点にする。ガードが無ければ一覧上の3回が先に届き、到達数が1を超える
  // The following pass-through wheel is the sync point: without the guard the three list wheels land first and the count exceeds one
  await page.mouse.move(1, 1);
  await page.mouse.wheel(0, 100);
  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBeGreaterThanOrEqual(before + 1);
  expect((await payloadsOf(page, "inventory.select_equipment")).length).toBe(before + 1);
});
