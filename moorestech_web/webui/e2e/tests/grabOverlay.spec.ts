import { test, expect } from "@playwright/test";

// 掴んだ瞬間からオーバーレイがカーソル位置に出ることを検証する（stale座標回帰の防止）
// Assert the overlay appears at the cursor from the very first held frame (guards the stale-position regression)
test("アイテムを掴んだ瞬間にオーバーレイがクリック座標へ表示される", async ({ page }) => {
  await page.goto("/");
  const slot = page.getByTestId("main-grid").locator("> div").first();
  await expect(slot).toBeVisible();
  const box = (await slot.boundingBox())!;
  await slot.click();
  const overlay = page.getByTestId("grab-overlay");
  await expect(overlay).toBeVisible();
  const overlayBox = (await overlay.boundingBox())!;
  // オーバーレイ原点はカーソル-24px。クリックはスロット中央なので 中央-24±2px に出るはず
  // The overlay origin is cursor-24px; the click hits the slot center, so expect center-24 (±2px)
  expect(Math.abs(overlayBox.x - (box.x + box.width / 2 - 24))).toBeLessThanOrEqual(2);
  expect(Math.abs(overlayBox.y - (box.y + box.height / 2 - 24))).toBeLessThanOrEqual(2);
});
