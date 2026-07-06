import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// 指定 type の action payload 一覧を返す。received は全テスト横断で蓄積されるため全等値で照合する
// List payloads of a given action type; received accumulates across tests, so match by full equality
const payloadsOf = async (page: import("@playwright/test").Page, type: string) => {
  const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
  return actions.filter((a) => a.type === type).map((a) => a.payload);
};

// 各テスト冒頭で chest を配信状態へリセットし、終了後は閉に戻して後続へ漏らさない
// Reset to chest before each test and back to closed afterwards so state never leaks
test.beforeEach(async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
});
test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

test("空手の右クリックで半分(切り捨て)を grab へ拾う", async ({ page }) => {
  // slot0 = Wood×7 → 半分は 3
  // slot0 = Wood x7, so half floors to 3
  await page.getByTestId("chest-grid").locator("> div").first().click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 3 });
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
});

test("grab 保持中の右クリックで block スロットへ1個置く", async ({ page }) => {
  // 左クリックで slot0 全量(7)を grab に取り、grab 反映を待ってから slot1 を右クリック
  // Left-click grabs all of slot0 (7); wait for the grab to reflect, then right-click slot1
  await page.getByTestId("chest-grid").locator("> div").first().click();
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
  await page.getByTestId("chest-grid").locator("> div").nth(1).click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "grab", slot: 0 }, to: { area: "block", slot: 1 }, count: 1 });
});

test("ダブルクリックで block_inventory.collect を送る", async ({ page }) => {
  await page.getByTestId("chest-grid").locator("> div").nth(1).dblclick();
  await expect
    .poll(() => payloadsOf(page, "block_inventory.collect"))
    .toContainEqual({ slot: { area: "block", slot: 1 } });
});

test("Shift+クリックで block→main へ配分移動する", async ({ page }) => {
  // slot0 = Wood×7。main0 が Wood×10(空き90) なので単一 move で全量入る
  // slot0 = Wood x7; main0 holds Wood x10 (room 90), so a single move takes it all
  await page.getByTestId("chest-grid").locator("> div").first().click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "main", slot: 0 }, count: 7 });
});

test("block 開時は main の Shift+クリックが block へ配分移動する", async ({ page }) => {
  // main1 = Stone×10。chest slot1 が Stone×4(空き96) なので block slot1 へ全量入る
  // main1 = Stone x10; chest slot1 holds Stone x4 (room 96), so it all goes to block slot1
  await page.getByTestId("main-grid").locator("> div").nth(1).click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "main", slot: 1 }, to: { area: "block", slot: 1 }, count: 10 });
});
