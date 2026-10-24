import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { cleanRoomYieldLines } from "../cleanRoomYieldLogic";

describe("cleanRoomYieldLines", () => {
  it("成膜は上限グレードと昇格率の2行を出す", () => {
    expect(cleanRoomYieldLines({ fetchState: "fetched", role: "waferGrade", maxGrade: 2, promotionChance: 0.9 })).toEqual([
      { testId: "clean-room-yield-max-grade", key: L.ui.blockInventory.cleanRoomYieldMaxGrade, params: { grade: "2" } },
      { testId: "clean-room-yield-promotion-chance", key: L.ui.blockInventory.cleanRoomYieldPromotionChance, params: { percent: "90.0" } },
    ]);
  });

  it("EUV は層の失敗率だけを出す", () => {
    expect(cleanRoomYieldLines({ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.02 })).toEqual([
      { testId: "clean-room-yield-layer-fail-rate", key: L.ui.blockInventory.cleanRoomYieldLayerFailRate, params: { percent: "2.0" } },
    ]);
  });

  it("ダイシングはダイの損失率だけを出す", () => {
    expect(cleanRoomYieldLines({ fetchState: "fetched", role: "dicing", dieLossRate: 0.03 })).toEqual([
      { testId: "clean-room-yield-die-loss-rate", key: L.ui.blockInventory.cleanRoomYieldDieLossRate, params: { percent: "3.0" } },
    ]);
  });

  it("割合は小数1桁の百分率に丸める", () => {
    const [, promotion] = cleanRoomYieldLines({ fetchState: "fetched", role: "waferGrade", maxGrade: 3, promotionChance: 0.12345 });
    expect(promotion.params).toEqual({ percent: "12.3" });
  });
});
