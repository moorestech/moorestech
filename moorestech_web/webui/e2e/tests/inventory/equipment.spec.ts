import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";

// 各枠はアンカー用ラッパーdiv内のスロット本体
// Each slot sits inside its anchor wrapper div
const equipmentSlots = (page: import("@playwright/test").Page) =>
  page.getByTestId("equipment-slots").locator("> div > div");

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

// 装備へアイテムを入れる唯一のUI経路。これが壊れると実プレイで装備が永久に空になり採掘が成立しない
// The only UI route that fills equipment; if it breaks, equipment stays empty forever in real play and mining never succeeds
test("メインのアイテムをUI操作だけで装備スロットへ移せる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();

  // main[1] を掴み、空の装備枠[1]へ置く
  // Pick up main[1] and drop it into the empty equipment slot [1]
  await page.getByTestId("main-grid").locator("> div").nth(1).click();
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
  await equipmentSlots(page).nth(1).click();

  // grab 保持中の左押下はドラッグ配分セッションになるため、単独スロットへの配置も split_drag で届く
  // A left press while holding grab opens the drag-allocation session, so even a single-slot drop arrives as split_drag
  await expect.poll(() => payloadsOf(page, "inventory.split_drag")).toContainEqual({
    slots: [{ area: "equipment", slot: 1 }],
  });
  await expect(equipmentSlots(page).nth(1)).toContainText("10");
  await expect(page.getByTestId("grab-overlay")).toHaveCount(0);
});

test("装備枠のクリックは選択ではなくアイテム移動になる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("equipment-slots")).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  // fixture の装備[0]は中身あり。空手クリックは掴み取りであって選択送信ではない
  // The fixture's equipment[0] is filled; an empty-handed click picks it up instead of selecting it
  await equipmentSlots(page).nth(0).click();

  await expect.poll(() => payloadsOf(page, "inventory.move_item")).toContainEqual({
    from: { area: "equipment", slot: 0 },
    to: { area: "grab", slot: 0 },
    count: 1,
  });
  expect((await payloadsOf(page, "inventory.select_equipment")).length).toBe(before);
});

test("装備へ移したアイテムをホイールで選択できる", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("main-grid").locator("> div").nth(1).click();
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
  await equipmentSlots(page).nth(1).click();
  await expect(equipmentSlots(page).nth(1)).toContainText("10");

  // 素手(-1)起点でホイール2段進めると装備[1]が選択状態になる
  // Starting from bare hands (-1), two wheel steps land the selection on equipment[1]
  for (let step = 0; step < 2; step++) {
    await page.mouse.wheel(0, 100);
    await expect(equipmentSlots(page).nth(step)).toHaveAttribute("data-selected", "true");
  }
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

// カーソルロック中のホイールは画面中央で起きる。中央にはクロスヘアが居るため、そこが本命の経路になる
// A wheel under cursor lock happens at the screen center, where the crosshair sits, so that is the primary route
test("画面中央のクロスヘアの上でもホイールで装備が切り替わる", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.locator('[data-tutorial-anchor="game.crosshair"]')).toBeVisible();
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  const viewport = page.viewportSize()!;
  await page.mouse.move(viewport.width / 2, viewport.height / 2);
  await page.mouse.wheel(0, 100);

  await expect.poll(async () => (await payloadsOf(page, "inventory.select_equipment")).length).toBe(before + 1);
});

// ホットバー帯はスロット列の左右が空箱のまま画面全幅に伸びるため、そこで入力を止めてはならない
// The hotbar band stretches full width with empty boxes beside the slot row, so it must not swallow input there
test("ホットバー帯のスロット列外でもホイールで装備が切り替わる", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const hotbar = page.getByTestId("hotbar-grid");
  await expect(hotbar).toBeVisible();
  const box = (await hotbar.boundingBox())!;
  const before = (await payloadsOf(page, "inventory.select_equipment")).length;

  await page.mouse.move(box.x / 2, box.y + box.height / 2);
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
