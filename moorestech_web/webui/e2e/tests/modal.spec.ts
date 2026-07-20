import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setModal } from "../support/mockControl";

// モーダルは opt-in。各テストは描画前に表示要求する
// The modal is opt-in; each test requests it before rendering
test.beforeEach(async ({ page }) => {
  await setModal(page, true);
});

// 後続テストファイルへグローバル modal 状態を漏らさない（backdrop が他テストを妨げる）
// Don't leak the global modal state into later test files (the backdrop would block them)
test.afterEach(async ({ page }) => {
  await setModal(page, false);
});

test("接続後にモーダルが描画される", async ({ page }) => {
  await page.goto("/");
  // dialog・タイトル・本文・ボタンが揃って表示される（タイトルは exact で本文の部分一致を避ける）
  // The dialog, title, message, and button are visible (title uses exact to avoid matching the message substring)
  await expect(page.getByTestId("modal")).toBeVisible();
  await expect(page.getByText("確認", { exact: true })).toBeVisible();
  await expect(page.getByText("これは確認ダイアログです")).toBeVisible();
  await expect(page.getByTestId("modal-button")).toHaveText("OK");
});

test("OK クリックで confirm を送りモーダルが消える", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("modal")).toBeVisible();
  await page.getByTestId("modal-button").click();
  // ui.modal.respond が {id, confirm} で送られる
  // ui.modal.respond is sent with {id, confirm}
  await expect
    .poll(async () => {
      const responds = await payloadsOf(page, "ui.modal.respond");
      return responds[responds.length - 1];
    })
    .toEqual({ id: "m1", result: "confirm" });
  // mock が modal:null を push し、モーダルが消える
  // The mock pushes modal:null and the modal disappears
  await expect(page.getByTestId("modal")).toHaveCount(0);
});

test("背景クリックで cancel を送る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("modal")).toBeVisible();
  // パネル外（背景）の左上をクリックして cancel を発火
  // Click the backdrop's top-left (outside the panel) to fire cancel
  await page.getByTestId("modal-backdrop").click({ position: { x: 5, y: 5 } });
  await expect
    .poll(async () => {
      const responds = await payloadsOf(page, "ui.modal.respond");
      return responds[responds.length - 1];
    })
    .toEqual({ id: "m1", result: "cancel" });
});
