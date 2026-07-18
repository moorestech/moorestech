import { test, expect } from "@playwright/test";
import { disconnectWebSockets, setSnapshotDelay } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setSnapshotDelay(page, 0, null);
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
