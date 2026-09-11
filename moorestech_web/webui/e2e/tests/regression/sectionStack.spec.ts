import { test, expect, type Page } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

const sectionIds = [
  "machine-section", "miner-section", "generator-section", "gear-section", "electric-network-section", "gear-network-section",
  "chest-grid", "machine-fluid-slots", "miner-output-grid", "gear-miner-output-grid", "generator-fuel-grid",
  "generic-block-grid", "generic-block-fluids", "filter-splitter", "pump-section", "pump-fluid-slots",
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
  { type: "pump", shown: ["pump-section", "electric-network-section", "pump-fluid-slots"] },
  { type: "gearPump", shown: ["pump-section", "gear-section", "gear-network-section", "pump-fluid-slots"] },
  { type: "gearGenerator", shown: ["gear-section", "gear-network-section"] },
] as const;

// 風車はconfigByBlockTypeでFuelGearGeneratorと同じ行を使うため、中身の無い燃料グリッドがDOMに残る
// The windmill shares FuelGearGenerator's configByBlockType row, so an empty fuel grid stays in the DOM
const renderedEmptyByType: Partial<Record<(typeof cases)[number]["type"], readonly string[]>> = {
  gearGenerator: ["generator-fuel-grid"],
};

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

for (const entry of cases) {
  test(`${entry.type}は対応セクションだけを表示する`, async ({ page }) => {
    await setBlock(page, entry.type);
    await page.goto("/");
    await expect(page.getByTestId("block-inventory")).toBeVisible();

    const renderedEmpty = renderedEmptyByType[entry.type] ?? [];
    for (const testId of entry.shown) await expect(page.getByTestId(testId)).toBeVisible();
    for (const testId of renderedEmpty) await expect(page.getByTestId(testId)).toBeAttached();
    for (const testId of sectionIds) {
      if (!entry.shown.includes(testId as never) && !renderedEmpty.includes(testId)) await expectAbsent(page, testId);
    }
  });
}

async function expectAbsent(page: Page, testId: string) {
  await expect(page.getByTestId(testId)).toHaveCount(0);
}
