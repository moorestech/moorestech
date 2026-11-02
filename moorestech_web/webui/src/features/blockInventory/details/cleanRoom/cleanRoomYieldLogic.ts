import type { CleanRoomYieldFetchedData } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

type CleanRoomYieldLine = { testId: string; key: TranslationKey; params: InterpolationValues };
type CleanRoomYieldRates =
  | { role: "layerExposure"; layerFailRate: number }
  | { role: "dicing"; dieLossRate: number; qualityTier: number; levelRates: number[]; failedRate: number };

// 役割に関係する行だけを作る。割合は小数1桁の百分率で出す
// Builds only the rows relevant to the role; rates are shown as percentages with one decimal
// 出力待ちの確定済み抽選があれば、その値と次の新規抽選の現在値を見出し付きで分けて並べる
// When a fixed draw waits for output, list its values and the next new draw's current values apart under headings
export function cleanRoomYieldLines(cleanRoomYield: CleanRoomYieldFetchedData): CleanRoomYieldLine[] {
  const next = rateLines(cleanRoomYield, "clean-room-yield");
  const pending = pendingRates(cleanRoomYield);
  if (pending === undefined) return next;
  return [
    { testId: "clean-room-yield-pending-label", key: L.ui.blockInventory.cleanRoomYieldPendingDrawLabel, params: {} },
    ...rateLines(pending, "clean-room-yield-pending"),
    { testId: "clean-room-yield-next-label", key: L.ui.blockInventory.cleanRoomYieldNextDrawLabel, params: {} },
    ...next,
  ];
}

// 確定済み抽選は親と同じ役割の値だけを持つので、親の役割を付けて同じ行の作り方へ渡す
// A fixed draw carries only the parent's role values, so tag it with the parent's role and reuse the same row builder
function pendingRates(cleanRoomYield: CleanRoomYieldFetchedData): CleanRoomYieldRates | undefined {
  switch (cleanRoomYield.role) {
    case "layerExposure":
      return cleanRoomYield.pendingDraw && { role: "layerExposure", ...cleanRoomYield.pendingDraw };
    case "dicing":
      return cleanRoomYield.pendingDraw && { role: "dicing", ...cleanRoomYield.pendingDraw };
  }
}

function rateLines(rates: CleanRoomYieldRates, testIdPrefix: string): CleanRoomYieldLine[] {
  switch (rates.role) {
    case "layerExposure":
      return [{ testId: `${testIdPrefix}-layer-fail-rate`, key: L.ui.blockInventory.cleanRoomYieldLayerFailRate, params: { percent: toPercent(rates.layerFailRate) } }];
    case "dicing":
      return dicingLines(rates, testIdPrefix);
  }
}

// ダイシングは品質Tier・損失率・0でないレベル割合・失敗ダイ割合の順。0のレベルは出ないので行を作らない
// Dicing lists quality tier, loss rate, non-zero level shares, then failed-die share; zero levels never appear, so they get no row
function dicingLines(rates: Extract<CleanRoomYieldRates, { role: "dicing" }>, testIdPrefix: string): CleanRoomYieldLine[] {
  const levelLines = rates.levelRates.flatMap((rate, index) =>
    rate === 0 ? [] : [{ testId: `${testIdPrefix}-level-rate-${index + 1}`, key: L.ui.blockInventory.cleanRoomYieldLevelRate, params: { level: String(index + 1), percent: toPercent(rate) } }],
  );
  return [
    { testId: `${testIdPrefix}-quality-tier`, key: L.ui.blockInventory.cleanRoomYieldQualityTier, params: { tier: String(rates.qualityTier) } },
    { testId: `${testIdPrefix}-die-loss-rate`, key: L.ui.blockInventory.cleanRoomYieldDieLossRate, params: { percent: toPercent(rates.dieLossRate) } },
    ...levelLines,
    { testId: `${testIdPrefix}-failed-rate`, key: L.ui.blockInventory.cleanRoomYieldFailedRate, params: { percent: toPercent(rates.failedRate) } },
  ];
}

function toPercent(rate: number): string {
  return (rate * 100).toFixed(1);
}
