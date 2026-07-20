import { test, expect, type Page } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

const sectionIds = [
  "machine-section", "miner-section", "generator-section", "gear-section", "electric-network-section", "gear-network-section",
  "chest-grid", "machine-fluid-slots", "miner-output-grid", "gear-miner-output-grid", "generator-fuel-grid",
  "generic-block-grid", "generic-block-fluids", "filter-splitter",
] as const;

const cases = [
  { type: "chest", shown: ["chest-grid"] },
  { type: "tank", shown: ["generic-block-fluids"] },
  { type: "machine", shown: ["machine-section", "electric-network-section", "machine-fluid-slots"] },
  { type: "gearMachine", shown: ["machine-section", "gear-section", "gear-network-section"] },
  { type: "miner", shown: ["miner-section", "electric-network-section", "miner-output-grid"] },
  { type: "gearMiner", shown: ["miner-section", "gear-section", "gear-network-section", "gear-miner-output-grid"] },
  { type: "generator", shown: ["generator-section", "electric-network-section", "generator-fuel-grid"] },
  { type: "generic", shown: ["generic-block-grid", "generic-block-fluids"] },
  { type: "filterSplitter", shown: ["filter-splitter"] },
] as const;

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

for (const entry of cases) {
  test(`${entry.type}は対応セクションだけを表示する`, async ({ page }) => {
    await setBlock(page, entry.type);
    await page.goto("/");
    await expect(page.getByTestId("block-inventory")).toBeVisible();

    for (const testId of entry.shown) await expect(page.getByTestId(testId)).toBeVisible();
    for (const testId of sectionIds) {
      if (!entry.shown.includes(testId as never)) await expectAbsent(page, testId);
    }
  });
}

async function expectAbsent(page: Page, testId: string) {
  await expect(page.getByTestId(testId)).toHaveCount(0);
}
