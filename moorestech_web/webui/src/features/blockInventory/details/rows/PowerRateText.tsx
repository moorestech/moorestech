import { computePowerRate } from "../detailLogic";
import LackHighlightText from "../LackHighlightText";
import { L, useI18n } from "@/shared/i18n";

// 電力率テキスト。不足赤は呼び出し側が許した時だけ（uGUI CommonMachineBlockStateDetail 準拠）
// Power-rate text, red when lacking only if the caller allows it (mirrors uGUI CommonMachineBlockStateDetail)
export default function PowerRateText({
  currentPower,
  requestPower,
  highlightShortage,
  testId,
}: {
  currentPower: number;
  requestPower: number;
  highlightShortage: boolean;
  testId: string;
}) {
  const { t } = useI18n();
  const rate = computePowerRate(currentPower, requestPower);
  return (
    <LackHighlightText insufficient={highlightShortage && rate < 1} size="sm" testId={testId}>
      {t(L.ui.blockInventory.powerRateSummary, {
        rate: Math.round(rate * 100),
        current: currentPower,
        required: requestPower,
      })}
    </LackHighlightText>
  );
}
