import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

// 画面幅20%の固定幅（tokens.cssの--notification-width）。丸め差だけを許容する
// Fixed at 20% of the screen width (--notification-width in tokens.css); only rounding slack is allowed
const WIDTH_RATIO = 0.2;
const WIDTH_TOLERANCE_PX = 1;
// 短文と長文の差がこの程度ないと「等幅」が偶然でないと言えない
// Without at least this much text-length gap, equal widths prove nothing
const MIN_TEXT_LENGTH_GAP = 15;

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだと他specを汚染する
  // Leaving uiState set pollutes other specs
  await setUiState(page, "PlayerInventory");
});

test("長さの違う通知が同時に出ても各行の横幅は画面幅比の固定値のまま", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");

  // 短文（Stone +5）と長文（前提条件の不足文）を同時に並べる
  // Put a short row (Stone +5) and a long row (the prerequisites-missing sentence) on screen together
  await setTopicScenario(page, "notificationItemEarned");
  await setTopicScenario(page, "notificationDenied");

  const rows = page.getByTestId("notification-row");
  await expect(rows).toHaveCount(2);

  const earned = page.locator('[data-testid="notification-row"][data-category="itemEarned"]');
  const denied = page.locator('[data-testid="notification-row"][data-category="operationDenied"]');
  await expect(earned).toHaveCount(1);
  await expect(denied).toHaveCount(1);

  // 文字数が近いと等幅が偶然でも成立してしまうため、まず差を確定させる
  // Similar text lengths would let equal widths pass by accident, so pin the gap first
  const earnedText = (await earned.innerText()).trim();
  const deniedText = (await denied.innerText()).trim();
  expect(deniedText.length - earnedText.length).toBeGreaterThanOrEqual(MIN_TEXT_LENGTH_GAP);

  const earnedBox = await earned.boundingBox();
  const deniedBox = await denied.boundingBox();
  expect(earnedBox).not.toBeNull();
  expect(deniedBox).not.toBeNull();

  const expectedWidth = page.viewportSize()!.width * WIDTH_RATIO;
  expect(Math.abs(earnedBox!.width - expectedWidth)).toBeLessThanOrEqual(WIDTH_TOLERANCE_PX);
  expect(Math.abs(deniedBox!.width - expectedWidth)).toBeLessThanOrEqual(WIDTH_TOLERANCE_PX);
});
