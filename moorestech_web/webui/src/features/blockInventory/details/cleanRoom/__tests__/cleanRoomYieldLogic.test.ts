import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { cleanRoomYieldLines } from "../cleanRoomYieldLogic";

describe("cleanRoomYieldLines", () => {
  it("EUV は層の失敗率だけを出す", () => {
    expect(cleanRoomYieldLines({ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.02 })).toEqual([
      { testId: "clean-room-yield-layer-fail-rate", key: L.ui.blockInventory.cleanRoomYieldLayerFailRate, params: { percent: "2.0" } },
    ]);
  });

  it("ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合を出す", () => {
    expect(cleanRoomYieldLines({ fetchState: "fetched", role: "dicing", dieLossRate: 0.03, qualityTier: 1, levelRates: [0.7, 0.1, 0], failedRate: 0.2 })).toEqual([
      { testId: "clean-room-yield-quality-tier", key: L.ui.blockInventory.cleanRoomYieldQualityTier, params: { tier: "1" } },
      { testId: "clean-room-yield-die-loss-rate", key: L.ui.blockInventory.cleanRoomYieldDieLossRate, params: { percent: "3.0" } },
      { testId: "clean-room-yield-level-rate-1", key: L.ui.blockInventory.cleanRoomYieldLevelRate, params: { level: "1", percent: "70.0" } },
      { testId: "clean-room-yield-level-rate-2", key: L.ui.blockInventory.cleanRoomYieldLevelRate, params: { level: "2", percent: "10.0" } },
      { testId: "clean-room-yield-failed-rate", key: L.ui.blockInventory.cleanRoomYieldFailedRate, params: { percent: "20.0" } },
    ]);
  });

  it("出力待ちの確定済み抽選があれば、確定時の値と次の新規抽選の現在値を見出し付きで分けて出す", () => {
    const lines = cleanRoomYieldLines({ fetchState: "fetched", role: "dicing", dieLossRate: 0.15, qualityTier: 0, levelRates: [0.2, 0, 0], failedRate: 0.8, pendingDraw: { dieLossRate: 0.03, qualityTier: 1, levelRates: [0.7, 0.1, 0], failedRate: 0.2 } });
    expect(lines.map((line) => [line.testId, line.params])).toEqual([
      ["clean-room-yield-pending-label", {}],
      ["clean-room-yield-pending-quality-tier", { tier: "1" }],
      ["clean-room-yield-pending-die-loss-rate", { percent: "3.0" }],
      ["clean-room-yield-pending-level-rate-1", { level: "1", percent: "70.0" }],
      ["clean-room-yield-pending-level-rate-2", { level: "2", percent: "10.0" }],
      ["clean-room-yield-pending-failed-rate", { percent: "20.0" }],
      ["clean-room-yield-next-label", {}],
      ["clean-room-yield-quality-tier", { tier: "0" }],
      ["clean-room-yield-die-loss-rate", { percent: "15.0" }],
      ["clean-room-yield-level-rate-1", { level: "1", percent: "20.0" }],
      ["clean-room-yield-failed-rate", { percent: "80.0" }],
    ]);
  });

  it("割合は小数1桁の百分率に丸める", () => {
    const [failRate] = cleanRoomYieldLines({ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.12345 });
    expect(failRate.params).toEqual({ percent: "12.3" });
  });
});
