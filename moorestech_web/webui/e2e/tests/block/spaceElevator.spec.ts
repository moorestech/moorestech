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
  await expect(page.getByTestId("space-elevator-grid")).toContainText("GPU衛星の入口");
  await expect(page.getByTestId("space-elevator-grid")).toContainText("成果の出口");
  await expect(page.getByTestId("space-elevator-input-grid").locator("> div")).toHaveCount(1);
  await expect(page.getByTestId("space-elevator-output-grid").locator("> div")).toHaveCount(2);
  await expect(page.getByTestId("electric-network-section")).toContainText("発電 4000 / 需要 2000");
  await expect(page.getByTestId("space-elevator-pool")).toContainText("1000");
  await page.getByTestId("space-elevator-connect").click();
  await expect.poll(async () => (await payloadsOf(page, "space_elevator.connect"))[0]).toEqual({});
  await expect(page.getByTestId("space-elevator-progress")).toBeVisible();
  await expect(page.getByTestId("space-elevator-connect")).toHaveCount(0);
});

// 不足・昇降・出口待ちを専用画面で検証
// Verify shortage, climbing and output waiting in the rendered dedicated view
for (const [fixture, status, progress] of [
  ["spaceElevatorInsufficient", "ケーブルが足りません", null],
  ["spaceElevatorClimbing", "昇降中", "0.5"],
  ["spaceElevatorOutputBlocked", "出口が満杯", "1"],
] as const) {
  test(`${fixture} の状態と進捗を描画する`, async ({ page }) => {
    await setBlock(page, fixture);
    await page.goto("/");
    await expect(page.getByTestId("space-elevator-view")).toBeVisible();
    await expect(page.getByTestId("space-elevator-grid")).toContainText("GPU衛星の入口");
    await expect(page.getByTestId("space-elevator-grid")).toContainText("成果の出口");
    await expect(page.getByTestId("electric-network-section")).toContainText("供給率 100%");
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

// 実ジェスチャから送信payloadと更新された在庫を検査する（積荷はmock代用品）
// Check sent payloads and updated inventory through real gestures (payload items are mock stand-ins)
test("入口への投入は元slot0を送り出口を変えない", async ({ page }) => {
  await setBlock(page, "spaceElevator");
  await page.goto("/");
  const input = page.getByTestId("space-elevator-input-grid").locator("> div").first();
  const output = page.getByTestId("space-elevator-output-grid").locator("> div");
  const before = (await payloadsOf(page, "block_inventory.move_item")).length;
  await page.getByTestId("main-grid").locator("> div").first().click();
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
  await input.click();
  await expect.poll(async () => (await payloadsOf(page, "block_inventory.move_item")).slice(before))
    .toEqual([{ from: { area: "grab", slot: 0 }, to: { area: "block", slot: 0 }, count: 10 }]);
  await expect(input).toContainText("10");
  await expect(page.getByTestId("grab-overlay")).toHaveCount(0);
  await expect(output.nth(0).locator(".iconTextOutlineLight")).toHaveText("4");
  await expect(output.nth(1).locator(".iconTextOutlineLight")).toHaveText("7");
});

for (const [outputIndex, count] of [[0, 4], [1, 7]] as const) {
  test(`出口${outputIndex + 1}の回収は元slot${outputIndex + 1}を送る`, async ({ page }) => {
    await setBlock(page, "spaceElevator");
    await page.goto("/");
    const output = page.getByTestId("space-elevator-output-grid").locator("> div");
    const before = (await payloadsOf(page, "block_inventory.move_item")).length;
    await output.nth(outputIndex).click();
    await expect.poll(async () => (await payloadsOf(page, "block_inventory.move_item")).slice(before))
      .toEqual([{ from: { area: "block", slot: outputIndex + 1 }, to: { area: "grab", slot: 0 }, count }]);
    await expect(page.getByTestId("grab-overlay")).toContainText(String(count));
    await expect(output.nth(outputIndex)).toHaveText("");
    await expect(output.nth(1 - outputIndex).locator(".iconTextOutlineLight")).toHaveText(outputIndex === 0 ? "7" : "4");
    await expect(page.getByTestId("space-elevator-input-grid")).toHaveText("");
  });
}

// 未着を説明し、到着後だけ接続操作を有効にする
// Explain pending details and enable connection only after they arrive
test("詳細の取得待ちから接続可能へ遷移する", async ({ page }, testInfo) => {
  await setBlock(page, "spaceElevatorLoading");
  await page.goto("/");
  await expect(page.getByTestId("space-elevator-loading")).toContainText("状態の取得を待っています");
  await expect(page.getByTestId("space-elevator-connect")).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath("space-elevator-loading.png") });
  await setBlock(page, "spaceElevator");
  await expect(page.getByTestId("space-elevator-loading")).toHaveCount(0);
  await expect(page.getByTestId("space-elevator-connect")).toBeEnabled();
  await page.screenshot({ path: testInfo.outputPath("space-elevator-ready.png") });
});
