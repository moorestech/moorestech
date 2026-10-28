import { describe, it, expect } from "vitest";

import { blockComponents, resolveBlockComponent } from "./blockComponentRegistry";
import SectionStackView from "../views/SectionStackView";
import { registeredBlockTypes } from "./registeredBlockTypes";

describe("resolveBlockComponent", () => {
  it("標準ビューのblockTypeは単一のフォールバックへ統合する", () => {
    const fallback = resolveBlockComponent("unknown").body;
    expect(resolveBlockComponent("Chest").body).toBe(fallback);
    expect(resolveBlockComponent("ElectricMachine").body).toBe(fallback);
    expect(resolveBlockComponent("GearMiner").body).toBe(fallback);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest").body).toBe(SectionStackView);
  });
  it("tank は登録キー削除済みのためフォールバックを返す", () => {
    expect(resolveBlockComponent("tank").body).toBe(SectionStackView);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown").body).toBe(SectionStackView);
  });

  it.each(["Shaft", "Gear", "GearBeltConveyor"])(
    "GearEnergyTransformerUI対象の%sを既存ギアビューへ明示登録する",
    (blockType) => {
      expect(blockComponents).toHaveProperty(blockType, { body: SectionStackView });
    },
  );

  it("スキーマに存在しないGearEnergyTransformerキーは登録しない", () => {
    expect(blockComponents).not.toHaveProperty("GearEnergyTransformer");
  });

  it("ElectricToGearGeneratorは専用ビューへ登録する", () => {
    expect(blockComponents).toHaveProperty("ElectricToGearGenerator");
    expect(blockComponents.ElectricToGearGenerator.body).not.toBe(SectionStackView);
  });

  it("SpaceElevatorBaseは接続ボタンを持つ専用ビューへ登録する", () => {
    expect(blockComponents).toHaveProperty("SpaceElevatorBase");
    expect(resolveBlockComponent("SpaceElevatorBase").body).not.toBe(SectionStackView);
  });


  it.each(["TrainStation", "TrainItemPlatform", "TrainFluidPlatform", "ElectricPole"])(
    "%sはPhase B2専用ビューへ登録する",
    (blockType) => {
      expect(blockComponents).toHaveProperty(blockType);
      expect(resolveBlockComponent(blockType).body).not.toBe(SectionStackView);
    },
  );

  it("網羅e2eが参照する登録blockType一覧と実レジストリを一致させる", () => {
    expect(Object.keys(blockComponents).sort()).toEqual([...registeredBlockTypes].sort());
  });
});
