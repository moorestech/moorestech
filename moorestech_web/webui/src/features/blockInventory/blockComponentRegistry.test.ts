import { describe, it, expect } from "vitest";

import { resolveBlockComponent } from "./blockComponentRegistry";
import ChestInventory from "./views/ChestInventory";
import TankInventory from "./views/TankInventory";
import GenericBlockInventory from "./views/GenericBlockInventory";

describe("resolveBlockComponent", () => {
  it("Chest(実マスタ値) は ChestInventory を返す", () => {
    expect(resolveBlockComponent("Chest")).toBe(ChestInventory);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest")).toBe(GenericBlockInventory);
  });
  it("tank は登録キー削除済みのためフォールバックを返す", () => {
    expect(resolveBlockComponent("tank")).toBe(GenericBlockInventory);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown")).toBe(GenericBlockInventory);
  });
  it.each([
    "ElectricMachine",
    "GearMachine",
    "ElectricGenerator",
    "FuelGearGenerator",
    "SimpleGearGenerator",
    "ElectricMiner",
    "GearMiner",
  ])("resolves a dedicated view for %s", (blockType) => {
    expect(resolveBlockComponent(blockType)).not.toBe(GenericBlockInventory);
  });
});

// TankInventory は専用 UI として温存（実流体ブロック配線時に再登録する想定）
// TankInventory is kept as a dedicated UI (to be re-registered when real fluid-block wiring lands)
describe("TankInventory", () => {
  it("コンポーネントとして存在する", () => {
    expect(TankInventory).toBeTypeOf("function");
  });
});
