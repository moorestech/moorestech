import { test, expect } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

// ブロック詳細5種が該当セクションを表示することを確認
// Verify each of the five block detail types renders its section
test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

const cases = [
  { type: "machine", testId: "machine-section" },
  { type: "gearMachine", testId: "gear-section" },
  { type: "generator", testId: "generator-section" },
  { type: "miner", testId: "miner-section" },
  { type: "filterSplitter", testId: "filter-splitter" },
] as const;

for (const { type, testId } of cases) {
  test(`renders ${type} detail section`, async ({ page }) => {
    await setBlock(page, type);
    await page.goto("/");
    await expect(page.getByTestId("block-inventory")).toBeVisible();
    await expect(page.getByTestId(testId)).toBeVisible();
  });
}

test("gear machine shows torque and gear network info", async ({ page }) => {
  await setBlock(page, "gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("gear-torque")).toContainText("トルク");
  await expect(page.getByTestId("gear-network-section")).toBeVisible();
});

test("機械recipeの出力個数と秒数から分間生産数を表示する", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("machine-items-per-minute")).toContainText("12");
});
