import { Text } from "@mantine/core";
import { computePowerRate } from "./detailLogic";

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
    <Text size="sm" c={rate < 1 ? "red.5" : "dark.1"} data-testid={testId}>
      電力 {Math.round(rate * 100)}% ({currentPower}/{requestPower})
    </Text>
  );
}
