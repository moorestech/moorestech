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
  { type: "pump", testId: "pump-section" },
  { type: "gearPump", testId: "pump-section" },
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

test("油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる", async ({ page }) => {
  await setBlock(page, "pump");
  await page.goto("/");
  await expect(page.getByTestId("pump-power-rate")).toContainText("100%");
  await expect(page.getByTestId("pump-pumping-fluids")).toContainText("3600.0");
  await expect(page.getByTestId("pump-no-vein")).toHaveCount(0);

  await setBlock(page, "pumpNoVein");
  await page.goto("/");
  await expect(page.getByTestId("pump-no-vein")).toBeVisible();
  await expect(page.getByTestId("pump-pumping-fluids")).toHaveCount(0);
});

test("歯車ポンプは電力行を持たず歯車行を出す", async ({ page }) => {
  await setBlock(page, "gearPump");
  await page.goto("/");
  await expect(page.getByTestId("gear-section")).toBeVisible();
  await expect(page.getByTestId("pump-power-rate")).toHaveCount(0);
});
