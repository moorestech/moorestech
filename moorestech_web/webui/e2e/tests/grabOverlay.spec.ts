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

// scale外でもgrab中心を守る
// Guard that the grab center matches the cursor even when scale is not 1
test("拡大表示でもgrab中心がカーソル位置へ追従する", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto("/");
  const slot = page.getByTestId("main-grid").locator("> div").first();
  await expect(slot).toBeVisible();
  await slot.click();
  const overlay = page.getByTestId("grab-overlay");
  await expect(overlay).toBeVisible();

  await page.mouse.move(1300, 620);
  const overlayBox = (await overlay.boundingBox())!;
  expect(Math.abs(overlayBox.x + overlayBox.width / 2 - 1300)).toBeLessThanOrEqual(2);
  expect(Math.abs(overlayBox.y + overlayBox.height / 2 - 620)).toBeLessThanOrEqual(2);
});
