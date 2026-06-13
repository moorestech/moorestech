import { test, expect } from "@playwright/test";

// 注: このテストは dev 専用 DebugActionButton に依存する。
// Task 6 で同ボタンを prod から外すため、本 spec は削除し toast 検証は vitest へ移管する。
// Note: depends on the dev-only DebugActionButton. Task 6 removes it from prod,
// so this spec is deleted then and toast verification moves to vitest.
test("Ping Action ボタンは成功トーストを出す", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Ping Action" }).click();
  await expect(page.getByText("debug.echo ok")).toBeVisible();
});
