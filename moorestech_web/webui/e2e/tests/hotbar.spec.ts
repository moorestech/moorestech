import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// ホットバーグリッドはページ上で2番目の .grid.grid-cols-9（1番目はメイン）
// The hotbar grid is the second .grid.grid-cols-9 on the page (the first is main)
const hotbarSlots = (page: import("@playwright/test").Page) =>
  page.locator(".grid.grid-cols-9").nth(1).locator("> div");

test("ホットバー slot 0 が初期選択（border-yellow-400）", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // fixture の selectedHotbar:0 により slot 0 が黄色枠
  // The fixture's selectedHotbar:0 highlights slot 0 with the yellow border
  await expect(hotbarSlots(page).nth(0)).toHaveClass(/border-yellow-400/);
  await expect(hotbarSlots(page).nth(1)).not.toHaveClass(/border-yellow-400/);
});

test('"2" キーで select_hotbar{index:1} を送り、選択が slot 1 へ移る', async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  await expect(hotbarSlots(page).nth(0)).toHaveClass(/border-yellow-400/);

  await page.keyboard.press("2");

  // host が select_hotbar を受理したことを __actions で確認
  // Confirm the host received select_hotbar via __actions
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      const sel = actions.find((a) => a.type === "inventory.select_hotbar");
      return sel?.payload as { index?: number } | undefined;
    })
    .toEqual({ index: 1 });

  // mock が selectedHotbar=1 で inventory event を push → slot 1 が選択、slot 0 は非選択
  // The mock pushes an inventory event with selectedHotbar=1 → slot 1 selected, slot 0 not
  await expect(hotbarSlots(page).nth(1)).toHaveClass(/border-yellow-400/);
  await expect(hotbarSlots(page).nth(0)).not.toHaveClass(/border-yellow-400/);
});
