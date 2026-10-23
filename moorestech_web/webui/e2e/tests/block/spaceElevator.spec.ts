import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("接続ボタンでactionを送り、接続後は進みを出す", async ({ page }) => {
  await setBlock(page, "spaceElevator");
  await page.goto("/");

  await expect(page.getByTestId("space-elevator-section")).toBeVisible();
  await expect(page.getByTestId("space-elevator-pool")).toContainText("1000");
  await page.getByTestId("space-elevator-connect").click();
  await expect.poll(async () => (await payloadsOf(page, "space_elevator.connect"))[0]).toEqual({});
  await expect(page.getByTestId("space-elevator-progress")).toBeVisible();
  await expect(page.getByTestId("space-elevator-connect")).toHaveCount(0);
});

// 不足・昇降・出口待ちを専用ビューの実描画で確かめる
// Exercise shortage, climbing, and output waiting through the real dedicated view
for (const [fixture, status, progress] of [
  ["spaceElevatorInsufficient", "ケーブルが足りません", null],
  ["spaceElevatorClimbing", "昇降中", "0.5"],
  ["spaceElevatorOutputBlocked", "出口が満杯", "1"],
] as const) {
  test(`${fixture} の状態と進捗を描画する`, async ({ page }) => {
    await setBlock(page, fixture);
    await page.goto("/");
    await expect(page.getByTestId("space-elevator-view")).toBeVisible();
    await expect(page.getByTestId("space-elevator-status")).toContainText(status);
    if (progress === null) {
      await expect(page.getByTestId("space-elevator-connect")).toBeDisabled();
      await expect(page.getByTestId("space-elevator-progress")).toHaveCount(0);
    } else {
      await expect(page.getByTestId("space-elevator-progress")).toHaveAttribute("aria-valuenow", progress);
      await expect(page.getByTestId("space-elevator-connect")).toHaveCount(0);
    }
  });
}
