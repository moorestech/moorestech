import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// 各テスト冒頭で chest を配信状態へリセットしてから接続する（決定的に panel を出すため）
// Reset the served block to chest before connecting so the panel deterministically shows
test("chest の block inventory パネルが描画される", async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByText("Chest")).toBeVisible();
  // 先頭スロット itemId=1,count=7 の count バッジが出る
  // The count badge for the first slot (itemId=1, count=7) appears
  await expect(page.getByText("7").first()).toBeVisible();
});

test("filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る", async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();

  // chest-grid 先頭(block slot 0, count=7)をクリックして拾い上げる
  // Click the first chest-grid slot (block slot 0, count=7) to pick it up
  const firstSlot = page.getByTestId("chest-grid").locator("> div").first();
  await firstSlot.click();

  // block→grab の move_item が送られたことを /__actions で検証
  // Verify the block→grab move_item was sent via /__actions
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      const move = actions.find((a) => a.type === "block_inventory.move_item");
      return move?.payload as
        | { from?: { area?: string; slot?: number }; to?: { area?: string; slot?: number }; count?: number }
        | undefined;
    })
    .toEqual({ from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 7 });

  // mock が grab を更新し inventory event を流すので grab オーバーレイ(fixed z-40)が出現する
  // The mock updates grab and pushes an inventory event, so the grab overlay (fixed z-40) appears
  await expect(page.locator(".fixed.z-40")).toBeVisible();
});

test("ブロックを閉じると panel が消える", async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();

  // closed を配信すると mock が open:false の event を流し panel が閉じる
  // Serving closed makes the mock push an open:false event so the panel closes
  await page.request.get("/__block?type=closed");
  await expect(page.getByTestId("block-inventory")).toHaveCount(0);
});
