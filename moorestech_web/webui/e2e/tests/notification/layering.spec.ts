import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { NOTIFICATION_DISPLAY_MS } from "../../../src/features/notification/notificationStore";

// サブピクセルの接触では検証に足る重なりと言えない。検出可能な面積を持つ重なりだけを採用する
// Sub-pixel contact isn't a real overlap for verification purposes; only accept an overlap with detectable area
const MIN_OVERLAP_PX = 4;

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだとCRAFT RECIPE系specが汚染される
  // Leaving uiState set pollutes the CRAFT RECIPE specs
  await setUiState(page, "PlayerInventory");
});

test("インベントリを開いている間、通知はパネルの描画を一切変えない", async ({ page }, testInfo) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const grid = page.getByTestId("main-grid");
  await expect(grid).toBeVisible();

  // スロット面は半透明で背面でも透けるため、実際に不透明なスロット（アイテム持ち・選択枠なし・不足表示なし）だけを比較先に固定する
  // Slot faces are translucent and show through even when behind, so pin comparisons to slots that are actually opaque (filled, unselected, sufficient)
  const opaqueFilledSlots = grid.locator('> div > div[data-filled="true"]:not([data-insufficient="true"]):not([data-selected="true"])');
  const opaqueCount = await opaqueFilledSlots.count();
  expect(opaqueCount).toBeGreaterThan(0);

  // アイテム画像はGameIconの非同期<img>。デコード未完了のまま撮ると層序と無関係な差分になる
  // Item images are GameIcon's async <img>; shooting before decode finishes yields diffs unrelated to layer order
  await grid.locator("img").evaluateAll((images) =>
    Promise.all(images.map((image) => (image as HTMLImageElement).decode().catch(() => undefined))));

  // ボックス位置は通知の有無で動かないため、撮影前に安価な座標だけを一括取得する
  // Box positions don't move with the notification, so gather cheap coordinates upfront before any screenshot
  const slotBoxes = await Promise.all(
    Array.from({ length: opaqueCount }, (_, i) => opaqueFilledSlots.nth(i).boundingBox()));

  await setTopicScenario(page, "notificationAchievement");
  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();

  // 通知が生存尺切れで消えている、または入場アニメ中だと比較が空振りするため、撮影直前に生存とopacity到達を確認する
  // Re-confirm the notification is alive and its enter animation has settled right before capturing, or the comparison goes vacuous either way
  await expect(row).toHaveCSS("opacity", "1");
  const rowBox = await row.boundingBox();
  expect(rowBox).not.toBeNull();

  // 重なっていなければこの検証は無意味になるため、まず実際の重なりを確定させる
  // Without an actual overlap the check is vacuous, so pin an actual intersection first
  let overlappingIndex = -1;
  for (let i = 0; i < opaqueCount; i++) {
    const box = slotBoxes[i];
    if (!box) continue;
    const overlapWidth = Math.min(box.x + box.width, rowBox!.x + rowBox!.width) - Math.max(box.x, rowBox!.x);
    const overlapHeight = Math.min(box.y + box.height, rowBox!.y + rowBox!.height) - Math.max(box.y, rowBox!.y);
    if (overlapWidth >= MIN_OVERLAP_PX && overlapHeight >= MIN_OVERLAP_PX) {
      // 背面にいるなら重なりを持つどの不透明スロットで検証しても結果は不変なので、最初の1件で十分
      // If it truly sits behind, any opaque overlapping slot proves the same thing, so the first hit suffices
      overlappingIndex = i;
      break;
    }
  }
  expect(overlappingIndex).toBeGreaterThanOrEqual(0);

  // before/afterの取得元を1つの要素ハンドルへ固定し、locatorの再解決によるスロット取り違えを避ける
  // Pin before/after capture to a single element handle so locator re-resolution can't swap the target slot
  const targetHandle = await opaqueFilledSlots.nth(overlappingIndex).elementHandle();
  expect(targetHandle).not.toBeNull();

  // 撮影は通知あり→なしの2枚だけに固定する（全件撮影して大半を捨てる無駄をしない）
  // Capture exactly two shots, notification-present then notification-absent (never shoot every slot and discard most)
  const withNotification = await targetHandle!.screenshot();

  // クリア通知は既存の生存尺タイマーを追い越せないため、退場アニメの自然完了を待って外す
  // A clear notification can't preempt the running lifetime timer, so wait for the exit animation to finish naturally
  await expect(page.getByTestId("notification-row")).toHaveCount(0, { timeout: NOTIFICATION_DISPLAY_MS + 2000 });
  const withoutNotification = await targetHandle!.screenshot();

  // 失敗時にどちらの画像が原因か目視できるよう、成果物として両方残す
  // Attach both images as artifacts so a failure can be inspected visually rather than trusting a bare boolean
  await testInfo.attach("slot-with-notification.png", { body: withNotification, contentType: "image/png" });
  await testInfo.attach("slot-without-notification.png", { body: withoutNotification, contentType: "image/png" });

  // 背面にいるなら不透明面の画素は通知の有無で変わらない
  // If it truly sits behind, the opaque slot's pixels are identical with and without the notification
  expect(withNotification.equals(withoutNotification)).toBe(true);
});

test("GameScreenでも通知はマウントされ続け、矩形を持つ", async ({ page }) => {
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

  // z値比較でトークン差し替え漏れを検知
  // Catches missed token-swap regressions via z comparison
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
