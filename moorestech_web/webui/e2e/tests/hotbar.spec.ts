import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// ホットバーグリッドは data-testid="hotbar-grid" で特定する
// The hotbar grid is identified via data-testid="hotbar-grid"
const hotbarSlots = (page: import("@playwright/test").Page) =>
  page.getByTestId("hotbar-grid").locator("> div");

test("ホットバー slot 0 が初期選択（data-selected）", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // fixture の selectedHotbar:0 により slot 0 が選択状態
  // The fixture's selectedHotbar:0 marks slot 0 as selected
  await expect(hotbarSlots(page).nth(0)).toHaveAttribute("data-selected", "true");
  await expect(hotbarSlots(page).nth(1)).not.toHaveAttribute("data-selected", "true");
});

test('"2" キーで select_hotbar{index:1} を送り、選択が slot 1 へ移る', async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  await expect(hotbarSlots(page).nth(0)).toHaveAttribute("data-selected", "true");

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
  await expect(hotbarSlots(page).nth(1)).toHaveAttribute("data-selected", "true");
  await expect(hotbarSlots(page).nth(0)).not.toHaveAttribute("data-selected", "true");
});
