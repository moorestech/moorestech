import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
const EARNED_ITEM_ID = 2;
// mock hostはアイコンを404で返すため、アイコン枠は#idフォールバック文字になる
// The mock host 404s icons, so the icon slot renders the #id fallback text
const ICON_FALLBACK = `#${EARNED_ITEM_ID}`;

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだとCRAFT RECIPE系specが汚染される
  // Leaving uiState set pollutes the CRAFT RECIPE specs
  await setUiState(page, "PlayerInventory");
});

test("アイテム獲得通知はアイコンと獲得数を出し連続獲得で数値が伸びる", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationItemEarned");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();
  // アイコン枠が獲得アイテムのidで解決され、その隣に獲得数が出る
  // The icon slot resolves from the earned item's id and the amount sits next to it
  await expect(row).toHaveText(`${ICON_FALLBACK}+5`);

  // 同一アイテムの再獲得は行を増やさず数値だけ伸ばす
  // Earning the same item again grows the number instead of adding a row
  await setTopicScenario(page, "notificationItemEarnedAgain");
  await expect(page.getByTestId("notification-row").first()).toHaveText(`${ICON_FALLBACK}+8`);
  await expect(page.getByTestId("notification-row")).toHaveCount(1);
});
