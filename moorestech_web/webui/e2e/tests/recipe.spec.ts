import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";

test("アイテム選択でレシピ表示、長押しで連続クラフト送信できる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Items" })).toBeVisible();
  // 右リストの先頭 Plank(100) を選択
  // Select the first item Plank(100) in the right list
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = page.getByRole("button", { name: "Craft" });
  await expect(craftButton).toBeEnabled();

  // ボタンを押し下げ保持して連続クラフトを発火させる
  // Hold the button down to fire continuous crafts
  const box = await craftButton.boundingBox();
  if (box === null) throw new Error("craft button has no bounding box");
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();

  // 保持中に複数回クラフト要求が送られることを確認（=連続クラフト）
  // Holding must send multiple craft requests (=continuous craft)
  await expect
    .poll(async () => (await payloadsOf(page, "craft.execute")).length, { timeout: 5000 })
    .toBeGreaterThanOrEqual(2);

  await page.mouse.up();

  // 全送信が対象レシピ宛であること
  // Every request must target the shown recipe
  const payloads = await payloadsOf(page, "craft.execute");
  for (const payload of payloads) {
    expect((payload as { recipeGuid?: string }).recipeGuid).toBe("g-craft-1");
  }
});
