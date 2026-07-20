import { describe, it, expect } from "vitest";

import { blockComponents, resolveBlockComponent } from "./blockComponentRegistry";
import SectionStackView from "../views/SectionStackView";
import { registeredBlockTypes } from "./registeredBlockTypes";

describe("resolveBlockComponent", () => {
  it("標準ビューのblockTypeは単一のフォールバックへ統合する", () => {
    const fallback = resolveBlockComponent("unknown");
    expect(resolveBlockComponent("Chest")).toBe(fallback);
    expect(resolveBlockComponent("ElectricMachine")).toBe(fallback);
    expect(resolveBlockComponent("GearMiner")).toBe(fallback);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest")).toBe(SectionStackView);
  });
  it("tank は登録キー削除済みのためフォールバックを返す", () => {
    expect(resolveBlockComponent("tank")).toBe(SectionStackView);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown")).toBe(SectionStackView);
  });

  it.each(["Shaft", "Gear", "GearBeltConveyor"])(
    "GearEnergyTransformerUI対象の%sを既存ギアビューへ明示登録する",
    (blockType) => {
      expect(blockComponents).toHaveProperty(blockType, SectionStackView);
    },
  );

  it("スキーマに存在しないGearEnergyTransformerキーは登録しない", () => {
    expect(blockComponents).not.toHaveProperty("GearEnergyTransformer");
  });

  it("ElectricToGearGeneratorは専用ビューへ登録する", () => {
    expect(blockComponents).toHaveProperty("ElectricToGearGenerator");
    expect(blockComponents.ElectricToGearGenerator).not.toBe(SectionStackView);
  });

  it.each(["TrainStation", "TrainItemPlatform", "TrainFluidPlatform", "ElectricPole"])(
    "%sはPhase B2専用ビューへ登録する",
    (blockType) => {
      expect(blockComponents).toHaveProperty(blockType);
      expect(resolveBlockComponent(blockType)).not.toBe(SectionStackView);
    },
  );

  it("網羅e2eが参照する登録blockType一覧と実レジストリを一致させる", () => {
    expect(Object.keys(blockComponents).sort()).toEqual([...registeredBlockTypes].sort());
  });
});
