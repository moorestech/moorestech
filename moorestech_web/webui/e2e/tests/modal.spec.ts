import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// モーダルは opt-in。各テストは描画前に /__modal?show=1 で表示要求する
// The modal is opt-in; each test requests it via /__modal?show=1 before rendering
test.beforeEach(async ({ page }) => {
  await page.request.get("/__modal?show=1");
});

// 後続テストファイルへグローバル modal 状態を漏らさない（backdrop が他テストを妨げる）
// Don't leak the global modal state into later test files (the backdrop would block them)
test.afterEach(async ({ page }) => {
  await page.request.get("/__modal?show=0");
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
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      // /__actions は全テスト横断で蓄積されるため、最新の respond を見る（前テストの confirm を拾わない）
      // /__actions accumulates across tests, so read the latest respond (don't pick up a prior test's confirm)
      const responds = actions.filter((a) => a.type === "ui.modal.respond");
      return responds[responds.length - 1]?.payload;
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
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      // /__actions は全テスト横断で蓄積されるため、最新の respond を見る（前テストの confirm を拾わない）
      // /__actions accumulates across tests, so read the latest respond (don't pick up a prior test's confirm)
      const responds = actions.filter((a) => a.type === "ui.modal.respond");
      return responds[responds.length - 1]?.payload;
    })
    .toEqual({ id: "m1", result: "cancel" });
});
