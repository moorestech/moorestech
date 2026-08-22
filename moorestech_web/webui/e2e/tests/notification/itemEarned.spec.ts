import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
const EARNED_ITEM_ID = 2;
// アイコンは404で#idフォールバックに
// The mock host 404s icons, so the icon slot renders the #id fallback text
const ICON_FALLBACK = `#${EARNED_ITEM_ID}`;
// itemId 2 = STONE_ITEM_GUID。mockの合成辞書が返す表示名
// itemId 2 is STONE_ITEM_GUID; this is the display name the mock's composite dictionary returns
const EARNED_ITEM_NAME = "Stone";

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだと他specを汚染する
  // Leaving uiState set pollutes other specs
  await setUiState(page, "PlayerInventory");
});

test("アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationItemEarned");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();
  // アイコンはitemId解決、隣にアイテム名と獲得数
  // The icon slot resolves from the earned item's id and the name and amount sit next to it
  await expect(row).toHaveText(`${ICON_FALLBACK}${EARNED_ITEM_NAME} +5`);

  // 再獲得は行を増やさず数値のみ伸ばす
  // Earning the same item again grows the number instead of adding a row
  await setTopicScenario(page, "notificationItemEarnedAgain");
  await expect(page.getByTestId("notification-row").first()).toHaveText(`${ICON_FALLBACK}${EARNED_ITEM_NAME} +8`);
  await expect(page.getByTestId("notification-row")).toHaveCount(1);
});
