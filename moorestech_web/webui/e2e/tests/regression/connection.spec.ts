import { test, expect } from "@playwright/test";
import { TIMBER_ITEM_GUID, WOOD_ITEM_GUID } from "../../mock-host/fixtures";
import { disconnectWebSockets, injectTopicSnapshot, setSnapshotDelay, setTopicScenario, setTopicScenarioRevision, setWoodItemGuid } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setSnapshotDelay(page, 0, null);
  await setTopicScenario(page, "miningHidden");
  await setWoodItemGuid(page, WOOD_ITEM_GUID);
});

test("切断後は全topic snapshotをrestoring中に復元し旧世代snapshotを破棄する", async ({ page }) => {
  await setTopicScenarioRevision(page, "mining", 10, 0.1);
  await page.goto("/");
  const progress = page.locator('[data-tutorial-anchor~="mining.hud"] [role="progressbar"]');
  await expect(progress).toHaveAttribute("aria-valuenow", "0.1");
  await setTopicScenarioRevision(page, "mining", 11, 0.65);
  await expect(progress).toHaveAttribute("aria-valuenow", "0.65");

  const firstSlot = page.getByTestId("main-grid").locator("> div").first();
  await firstSlot.click();
  await expect(page.getByTestId("grab-overlay")).toContainText("10");
  await setSnapshotDelay(page, 800, "ui.progress");
  await disconnectWebSockets(page, 200);
  await setWoodItemGuid(page, TIMBER_ITEM_GUID);
  const overlay = page.getByTestId("reconnect-overlay");
  await expect(overlay).toBeVisible();

  // 全snapshot後だけopenへ戻る
  // Return open only after every snapshot
  await expect(overlay).toBeHidden({ timeout: 5000 });
  await expect(progress).toHaveAttribute("aria-valuenow", "0.65");
  await expect(page.getByTestId("grab-overlay")).toContainText("10");
  await page.getByTestId("main-grid").locator("> div").nth(2).hover();
  await expect(page.getByRole("tooltip")).toContainText("Timber");

  // 旧snapshotの上書きを防ぐ
  // Prevent an old snapshot from rolling back the UI
  await injectTopicSnapshot(page, "mining", 10, 0.2);
  await expect(progress).toHaveAttribute("aria-valuenow", "0.65");
});

test("inventory未受信中はconnecting placeholderを表示する", async ({ page }) => {
  await setSnapshotDelay(page, 1000, "local_player.inventory");
  await page.goto("/");

  // inventoryとrecipeは同じ未受信状態をそれぞれplaceholderで表す
  // Inventory and recipe each expose the same pending state through a placeholder
  await expect(page.getByText("connecting...", { exact: true })).toHaveCount(2);
  await expect(page.getByText("connecting...", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("connecting...", { exact: true }).last()).toBeVisible();
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible({ timeout: 3000 });
});

test("WS切断時は再接続バナーをz-reconnect層に表示する", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
  await disconnectWebSockets(page, 1000);

  const overlay = page.getByTestId("reconnect-overlay");
  await expect(overlay).toBeVisible();
  await expect(overlay).toContainText("再接続中...");
  await expect(overlay).toHaveCSS("z-index", "2000");
});
