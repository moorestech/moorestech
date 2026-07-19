import { computePowerRate } from "./detailLogic";
import LackHighlightText from "./LackHighlightText";
import { useI18n } from "@/shared/i18n";

// 電力率テキスト。不足時は赤表示（uGUI CommonMachineBlockStateDetail 準拠）
// Power-rate text, red when lacking (mirrors uGUI CommonMachineBlockStateDetail)
export default function PowerRateText({
  currentPower,
  requestPower,
  testId,
}: {
  currentPower: number;
  requestPower: number;
  testId: string;
}) {
  const { t } = useI18n();
  const rate = computePowerRate(currentPower, requestPower);
  return (
    <LackHighlightText label={t("電力 {rate}% (", { rate: Math.round(rate * 100) })} current={currentPower} separator={t("/")} required={requestPower} suffix={t(")")} insufficient={rate < 1} size="sm" testId={testId} />
  );
}
