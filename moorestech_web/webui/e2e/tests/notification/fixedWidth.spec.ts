import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

// --notification-widthの256px（基準幅1280の20%）
// The 256px of --notification-width (20% of the 1280 reference width)
const EXPECTED_WIDTH_PX = 256;
const WIDTH_TOLERANCE_PX = 1;
// 等幅が偶然でないと言える文字数差
// The text-length gap that makes equal widths meaningful
const MIN_TEXT_LENGTH_GAP = 15;

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだと他specを汚染する
  // Leaving uiState set pollutes other specs
  await setUiState(page, "PlayerInventory");
});

test("長さの違う通知が同時に出ても各行の横幅は固定値のまま", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");

  // 短文と長文を同時に並べる
  // Put a short row and a long row on screen together
  await setTopicScenario(page, "notificationItemEarned");
  await setTopicScenario(page, "notificationDenied");

  const rows = page.getByTestId("notification-row");
  await expect(rows).toHaveCount(2);

  const earned = page.locator('[data-testid="notification-row"][data-category="itemEarned"]');
  const denied = page.locator('[data-testid="notification-row"][data-category="operationDenied"]');
  await expect(earned).toHaveCount(1);
  await expect(denied).toHaveCount(1);

  // 先に文字数差を確定させる
  // Pin the text-length gap first
  const earnedText = (await earned.innerText()).trim();
  const deniedText = (await denied.innerText()).trim();
  expect(deniedText.length - earnedText.length).toBeGreaterThanOrEqual(MIN_TEXT_LENGTH_GAP);

  const earnedBox = await earned.boundingBox();
  const deniedBox = await denied.boundingBox();
  expect(earnedBox).not.toBeNull();
  expect(deniedBox).not.toBeNull();

  expect(Math.abs(earnedBox!.width - EXPECTED_WIDTH_PX)).toBeLessThanOrEqual(WIDTH_TOLERANCE_PX);
  expect(Math.abs(deniedBox!.width - EXPECTED_WIDTH_PX)).toBeLessThanOrEqual(WIDTH_TOLERANCE_PX);
});
