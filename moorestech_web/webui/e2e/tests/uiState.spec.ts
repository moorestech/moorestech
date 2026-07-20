import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setBlock, setUiState } from "../support/mockControl";

// 各テスト後に既定状態へ戻し、他 spec へ画面状態を漏らさない
// Reset to defaults after each test so screen state never leaks into other specs
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setBlock(page, "closed");
});

test("既定(PlayerInventory)でインベントリ画面が表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
});

test("GameScreen でパネルが消え、PlayerInventory への event で再表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();

  await setUiState(page, "GameScreen");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeHidden();

  await setUiState(page, "PlayerInventory");
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
});

test("SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
  // RECIPE_LOCATOR: recipe.spec.ts と同一セレクタで RecipeViewer の非表示を検証する
  // RECIPE_LOCATOR: assert the RecipeViewer is hidden using the same selector as recipe.spec.ts
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeHidden();
});

test("ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();

  await page.getByTestId("block-inventory-close").click();

  // action 送信契約を action 記録で検証する
  // Verify the send contract via the action log
  await expect
    .poll(async () => {
      return (await payloadsOf(page, "ui_state.request")).some((payload) => (payload as { state?: string }).state === "GameScreen");
    })
    .toBe(true);

  // mock が ui_state/block event を返し、インベントリ画面とブロックパネルが閉じる
  // The mock pushes ui_state/block events back, closing the inventory screen and block panel
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeHidden();
  await expect(page.getByTestId("block-inventory")).toBeHidden();
});

test("許可表で拒否されたui_state.requestはtransition_not_allowedを返し画面を維持する", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  const result = await page.evaluate(async () => {
    const socket = new WebSocket(`ws://${location.host}/ws`);
    await new Promise<void>((resolve) => socket.addEventListener("open", () => resolve(), { once: true }));
    socket.send(JSON.stringify({ op: "action", type: "ui_state.request", requestId: "forbidden-transition", payload: { state: "PauseMenu" } }));
    return new Promise<{ ok: boolean; error?: string }>((resolve) => {
      socket.addEventListener("message", (event) => {
        const message = JSON.parse(String(event.data)) as { op?: string; requestId?: string; ok?: boolean; error?: string };
        if (message.op !== "result" || message.requestId !== "forbidden-transition") return;
        socket.close();
        resolve({ ok: message.ok === true, error: message.error });
      });
    });
  });

  expect(result).toEqual({ ok: false, error: "transition_not_allowed" });
  await expect.poll(async () => (await payloadsOf(page, "ui_state.request")).at(-1)).toEqual({ state: "PauseMenu" });
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByRole("heading", { name: "持ち物" })).toBeVisible();
});
