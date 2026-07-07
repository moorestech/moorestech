import { describe, it, expect, vi } from "vitest";

// 解決対象コンポーネントは BlockItemGrid 経由で webSocketClient を読み込む。
// import 時に location.host を触るため node 環境では stub する
// The resolved components import webSocketClient via BlockItemGrid, which touches location.host at import;
// stub it so this node-env test can load the component tree
vi.mock("@/bridge/transport/webSocketClient", () => ({ sendAction: vi.fn() }));

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
