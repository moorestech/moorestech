import { expect, test } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "pauseConnected");
  await setUiState(page, "PlayerInventory");
});

test("PauseMenu遷移で表示しセーブとセーブして終了actionを送る", async ({ page }) => {
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

test("トップから設定画面へ進み言語を選べ、戻るでトップへ戻る", async ({ page }) => {
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  const menu = page.getByTestId("pause-menu");
  await expect(menu.getByTestId("language-select")).toHaveCount(0);

  await menu.getByTestId("pause-menu-open-settings").click();
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "settings" });
  await expect(menu.getByTestId("language-select")).toBeVisible();

  await menu.getByTestId("pause-menu-back").click();
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "top" });
  await expect(menu.getByTestId("pause-menu-open-bug-report")).toBeVisible();
});

test("トップからバグ報告画面へ進むと報告欄が出る", async ({ page }) => {
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  const menu = page.getByTestId("pause-menu");
  await expect(menu.getByTestId("bug-report-description")).toHaveCount(0);

  await menu.getByTestId("pause-menu-open-bug-report").click();
  await expect(menu.getByTestId("bug-report-description")).toBeVisible();
});
