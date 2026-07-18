import { computePowerRate } from "./detailLogic";
import LackHighlightText from "./LackHighlightText";

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
  const rate = computePowerRate(currentPower, requestPower);
  return (
    <LackHighlightText label={`電力 ${Math.round(rate * 100)}% (`} current={currentPower} separator="/" required={requestPower} suffix=")" insufficient={rate < 1} normalColor="dark.1" insufficientColor="red.5" size="sm" testId={testId} />
  );
}
