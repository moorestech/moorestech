import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  await setUiState(page, "PlayerInventory");
});

test("インベントリを開いている間、通知はパネルの描画を一切変えない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const grid = page.getByTestId("main-grid");
  await expect(grid).toBeVisible();

  // スロット・パネル面は半透明なので背面でも透けて見える。完全不透明な filled スロットだけを比較先に固定する
  // Slot and panel faces are translucent and show through even when behind; pin the comparison to fully opaque filled slots only
  const filledSlots = grid.locator('> div[data-filled="true"]');
  const filledCount = await filledSlots.count();
  expect(filledCount).toBeGreaterThan(0);
  const beforeShots = await Promise.all(
    Array.from({ length: filledCount }, (_, i) => filledSlots.nth(i).screenshot()),
  );

  await setTopicScenario(page, "notificationAchievement");
  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();
  const rowBox = await row.boundingBox();
  expect(rowBox).not.toBeNull();

  // 重なっていなければこの検証は無意味になるため、まず filled スロットとの実際の重なりを確定させる
  // Without an actual overlap the check is vacuous, so pin an actual intersection against a filled slot first
  let overlappingIndex = -1;
  for (let i = 0; i < filledCount; i++) {
    const box = await filledSlots.nth(i).boundingBox();
    if (!box) continue;
    const overlapWidth = Math.min(box.x + box.width, rowBox!.x + rowBox!.width) - Math.max(box.x, rowBox!.x);
    const overlapHeight = Math.min(box.y + box.height, rowBox!.y + rowBox!.height) - Math.max(box.y, rowBox!.y);
    if (overlapWidth > 0 && overlapHeight > 0) {
      overlappingIndex = i;
      break;
    }
  }
  expect(overlappingIndex).toBeGreaterThanOrEqual(0);

  // 背面にいるなら不透明面の画素は通知の有無で変わらない
  // If it truly sits behind, the opaque slot's pixels are identical with and without the notification
  const after = await filledSlots.nth(overlappingIndex).screenshot();
  expect(after.equals(beforeShots[overlappingIndex])).toBe(true);
});

test("GameScreenでは通知が遮られず読める", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();
  const box = await row.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThan(0);
  expect(box!.height).toBeGreaterThan(0);
});

test("通知ホストはstageより後ろの層に立つ", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");
  await expect(page.getByTestId("notification-host")).toBeVisible();

  // 算出済みのz値で層序を確認する（トークン差し替えの取りこぼしを拾う）
  // Compare the computed z values so a missed token swap is caught
  const layers = await page.evaluate(() => {
    const host = document.querySelector('[data-testid="notification-host"]') as HTMLElement;
    const stage = document.querySelector('[data-testid="app-stage"]') as HTMLElement;
    return {
      sameParent: host.parentElement === stage.parentElement,
      hostZ: Number.parseInt(getComputedStyle(host).zIndex, 10),
      stageZ: Number.parseInt(getComputedStyle(stage).zIndex, 10),
    };
  });
  expect(layers.sameParent).toBe(true);
  expect(layers.hostZ).toBeLessThan(layers.stageZ);
});
