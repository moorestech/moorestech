import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

test("アイテム選択でレシピ表示、craft 可能なら送信できる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Items" })).toBeVisible();
  // 右リストの先頭 Plank(100) を選択
  // Select the first item Plank(100) in the right list
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await expect(page.getByRole("button", { name: "Craft" })).toBeEnabled();
  await page.getByRole("button", { name: "Craft" }).click();
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.some((a) => a.type === "craft.execute");
    })
    .toBe(true);
});
