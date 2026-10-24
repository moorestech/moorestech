import type { CleanRoomYieldData } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

type CleanRoomYieldLine = { testId: string; key: TranslationKey; params: InterpolationValues };

// 役割に関係する行だけを作る。割合は小数1桁の百分率で出す
// Builds only the rows relevant to the role; rates are shown as percentages with one decimal
export function cleanRoomYieldLines(cleanRoomYield: CleanRoomYieldData): CleanRoomYieldLine[] {
  switch (cleanRoomYield.role) {
    case "waferGrade":
      return [
        { testId: "clean-room-yield-max-grade", key: L.ui.blockInventory.cleanRoomYieldMaxGrade, params: { grade: String(cleanRoomYield.maxGrade) } },
        { testId: "clean-room-yield-promotion-chance", key: L.ui.blockInventory.cleanRoomYieldPromotionChance, params: { percent: toPercent(cleanRoomYield.promotionChance) } },
      ];
    case "layerExposure":
      return [{ testId: "clean-room-yield-layer-fail-rate", key: L.ui.blockInventory.cleanRoomYieldLayerFailRate, params: { percent: toPercent(cleanRoomYield.layerFailRate) } }];
    case "dicing":
      return [{ testId: "clean-room-yield-die-loss-rate", key: L.ui.blockInventory.cleanRoomYieldDieLossRate, params: { percent: toPercent(cleanRoomYield.dieLossRate) } }];
  }
}

function toPercent(rate: number): string {
  return (rate * 100).toFixed(1);
}
