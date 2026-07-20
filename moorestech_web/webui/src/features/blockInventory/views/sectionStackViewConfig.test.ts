import { describe, expect, it } from "vitest";

import { resolveSectionStackViewConfig } from "./SectionStackView";

describe("resolveSectionStackViewConfig", () => {
  it.each([
    ["Chest", "chest-grid", null, true, true],
    ["ElectricMachine", null, "machine-fluid-slots", false, false],
    ["GearMachine", null, "machine-fluid-slots", false, false],
    ["ElectricGenerator", "generator-fuel-grid", null, true, true],
    ["FuelGearGenerator", "generator-fuel-grid", null, true, true],
    ["SimpleGearGenerator", "generator-fuel-grid", null, true, true],
    ["ElectricMiner", "miner-output-grid", null, true, true],
    ["GearMiner", "gear-miner-output-grid", null, true, true],
    ["unknown", "generic-block-grid", "generic-block-fluids", false, true],
  ])("%s の従来testIdを維持する", (blockType, itemGridTestId, fluidRowTestId, renderEmptyGrid, showFluidProgress) => {
    expect(resolveSectionStackViewConfig(blockType)).toEqual({ itemGridTestId, fluidRowTestId, renderEmptyGrid, showFluidProgress });
  });
});
