import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

test("接続後にインベントリが描画される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // Wood(itemId=1,count=10) の count バッジが出る
  // The count badge for Wood (itemId=1, count=10) appears
  await expect(page.getByText("10").first()).toBeVisible();
});

test("左クリックで grab オーバーレイが追従する", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click();
  // move_item→grab を mock がシミュレートし、grab オーバーレイ(fixed z-40)が出現する
  // The mock simulates move_item→grab so the grab overlay (fixed z-40) appears
  await expect(page.locator(".fixed.z-40")).toBeVisible();
});

test("右クリックで inventory.split を送る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click({ button: "right" });
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.some((a) => a.type === "inventory.split");
    })
    .toBe(true);
});
