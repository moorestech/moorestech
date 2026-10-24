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

  it("出力待ちの確定済み抽選があれば、確定時の値と次の新規抽選の現在値を見出し付きで分けて出す", () => {
    const lines = cleanRoomYieldLines({ fetchState: "fetched", role: "waferGrade", maxGrade: 1, promotionChance: 0, pendingDraw: { maxGrade: 2, promotionChance: 0.9 } });
    expect(lines).toEqual([
      { testId: "clean-room-yield-pending-label", key: L.ui.blockInventory.cleanRoomYieldPendingDrawLabel, params: {} },
      { testId: "clean-room-yield-pending-max-grade", key: L.ui.blockInventory.cleanRoomYieldMaxGrade, params: { grade: "2" } },
      { testId: "clean-room-yield-pending-promotion-chance", key: L.ui.blockInventory.cleanRoomYieldPromotionChance, params: { percent: "90.0" } },
      { testId: "clean-room-yield-next-label", key: L.ui.blockInventory.cleanRoomYieldNextDrawLabel, params: {} },
      { testId: "clean-room-yield-max-grade", key: L.ui.blockInventory.cleanRoomYieldMaxGrade, params: { grade: "1" } },
      { testId: "clean-room-yield-promotion-chance", key: L.ui.blockInventory.cleanRoomYieldPromotionChance, params: { percent: "0.0" } },
    ]);
  });

  it("ダイシングの確定済み抽選も同じ役割の損失率で出す", () => {
    const lines = cleanRoomYieldLines({ fetchState: "fetched", role: "dicing", dieLossRate: 0.15, pendingDraw: { dieLossRate: 0.03 } });
    expect(lines.map((line) => [line.testId, line.params])).toEqual([
      ["clean-room-yield-pending-label", {}],
      ["clean-room-yield-pending-die-loss-rate", { percent: "3.0" }],
      ["clean-room-yield-next-label", {}],
      ["clean-room-yield-die-loss-rate", { percent: "15.0" }],
    ]);
  });

  it("割合は小数1桁の百分率に丸める", () => {
    const [, promotion] = cleanRoomYieldLines({ fetchState: "fetched", role: "waferGrade", maxGrade: 3, promotionChance: 0.12345 });
    expect(promotion.params).toEqual({ percent: "12.3" });
  });
});
