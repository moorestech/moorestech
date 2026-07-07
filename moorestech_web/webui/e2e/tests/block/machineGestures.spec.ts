import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

// 機械の入出力スロットにもチェストと同じフル操作（右クリ/Shift/収集）が効くことを検証する
// Assert machine in/out slots support the full gesture set (right-click/Shift/collect) like chests
test.beforeEach(async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("machine-section")).toBeVisible();
});
test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("機械入力スロットの空手右クリックで block_inventory.split を送る", async ({ page }) => {
  // input slot0 = itemId3×5。半分計算はホスト側（count はもう送らない）
  // input slot0 = itemId3 x5; the host computes the half (no count is sent anymore)
  await page.getByTestId("machine-input-slots").locator("> div").first().click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.split"))
    .toContainEqual({ from: { area: "block", slot: 0 } });
});

test("機械出力スロットのダブルクリックで collect を送る", async ({ page }) => {
  // 出力は slotLayout.input=2 の直後 → 統合 index 2
  // Output starts right after slotLayout.input=2, i.e. combined index 2
  await page.getByTestId("machine-output-slots").locator("> div").first().dblclick();
  await expect
    .poll(() => payloadsOf(page, "block_inventory.collect"))
    .toContainEqual({ slot: { area: "block", slot: 2 } });
});

test("機械入力スロットの Shift+クリックで main へ配分移動する", async ({ page }) => {
  // main に itemId3 のスタックが無いため最初の空きスロット(index3)へ全量5
  // main holds no itemId3 stack, so all 5 go to the first empty slot (index 3)
  await page.getByTestId("machine-input-slots").locator("> div").first().click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "main", slot: 3 }, count: 5 });
});
