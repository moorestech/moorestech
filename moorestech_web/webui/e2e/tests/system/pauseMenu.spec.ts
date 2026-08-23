import { expect, test } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "pauseConnected");
  await setUiState(page, "PlayerInventory");
});

test("PauseMenu遷移で表示しセーブとメインメニュー復帰actionを送る", async ({ page }) => {
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  const menu = page.getByTestId("pause-menu");
  await expect(menu).toBeVisible();

  await menu.getByRole("button", { name: "ゲームをセーブする" }).click();
  await menu.getByRole("button", { name: "セーブして終了" }).click();
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.save")).at(-1)).toEqual({});
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.save_and_quit")).at(-1)).toEqual({});
});

test("pause_menu.currentの切断状態を表示する", async ({ page }) => {
  await setTopicScenario(page, "pauseDisconnected");
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  await expect(page.getByTestId("pause-menu")).toContainText("サーバーから切断されました");
});
