import { test, expect, type Page } from "@playwright/test";
import { allActions } from "../../../support/actions";
import { setBlock } from "../../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

// main0 は Wood（itemId 1）×10。昇降機の入れ口が受け付けるのは itemId 5 だけなので、受け付ける枠が無い
// main0 holds Wood (itemId 1) x10; the elevator inlet accepts only itemId 5, so no slot accepts it
async function shiftClickUnacceptedWood(page: Page) {
  const warnings: string[] = [];
  page.on("console", (message) => { if (message.type() === "warning") warnings.push(message.text()); });
  const sentBefore = (await allActions(page)).length;
  const woodSlot = page.getByTestId("main-grid").locator("> div").first();
  await expect(woodSlot).toContainText("10");

  await woodSlot.click({ modifiers: ["Shift"] });

  // サーバー拒否と同じ文言の通知が出て、開発者向けの理由がログに残る
  // The same notice the server raises for this refusal appears, and the developer-facing reason is logged
  const denied = page.locator('[data-testid="notification-row"][data-category="operationDenied"]');
  await expect(denied).toContainText("このスロットには置けません");
  expect(warnings.some((text) => text.includes("[inventory.shift_move] not sent"))).toBe(true);
  // 何も送らず、持ち物はそのまま
  // Nothing is sent and the inventory stays as it was
  expect((await allActions(page)).slice(sentBefore)).toEqual([]);
  await expect(woodSlot).toContainText("10");
}

test("受け付けないアイテムのShiftは送らずに拒否の通知を出す", async ({ page }) => {
  await setBlock(page, "spaceElevator");
  await page.goto("/");
  await expect(page.getByTestId("space-elevator-section")).toBeVisible();

  await shiftClickUnacceptedWood(page);
});

test("詳細の取得待ちのShiftも送らずに拒否の通知を出し、取得中の表示を保つ", async ({ page }) => {
  await setBlock(page, "spaceElevatorLoading");
  await page.goto("/");
  await expect(page.getByTestId("space-elevator-loading")).toBeVisible();

  await shiftClickUnacceptedWood(page);
  await expect(page.getByTestId("space-elevator-loading")).toContainText("状態の取得を待っています");
});
