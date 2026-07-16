import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";

test("アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
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

  // mock が素材を消費するため、保持し続けると素材が尽きてボタンが disabled 化する
  // The mock consumes materials, so holding until they run out disables the button
  await expect(craftButton).toBeDisabled({ timeout: 5000 });
  await page.mouse.up();

  // 連続で複数回クラフト要求が送られ、全て対象レシピ宛であること
  // Multiple craft requests were sent continuously, all targeting the shown recipe
  const payloads = await payloadsOf(page, "craft.execute");
  expect(payloads.length).toBeGreaterThanOrEqual(2);
  for (const payload of payloads) {
    expect((payload as { recipeGuid?: string }).recipeGuid).toBe("g-craft-1");
  }
});

test("押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = page.getByRole("button", { name: "Craft" });
  await expect(craftButton).toBeEnabled();
  const box = await craftButton.boundingBox();
  if (box === null) throw new Error("craft button has no bounding box");

  // これまでの送信数を控える（recorder はテスト横断で蓄積するため差分で判定）
  // Snapshot the prior send count (the recorder accumulates across tests, so assert on the delta)
  const before = (await payloadsOf(page, "craft.execute")).length;

  // 進捗が半分ほど溜まる程度だけ保持（craftTime=0.2s 未満）してからボタン外へ移動
  // Hold only long enough to fill the arrow partway (< craftTime=0.2s), then move off the button
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.waitForTimeout(100);
  await page.mouse.move(box.x + box.width / 2, box.y - 100);

  // 外れた時点で経過がリセットされるため、craftTime を超えて待っても1回もクラフトされない
  // Leaving resets the elapsed time, so nothing crafts even after waiting past craftTime
  await page.waitForTimeout(500);
  await page.mouse.up();
  const after = (await payloadsOf(page, "craft.execute")).length;
  expect(after).toBe(before);
});
