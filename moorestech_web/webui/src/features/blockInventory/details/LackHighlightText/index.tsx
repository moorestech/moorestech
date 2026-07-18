import type { ReactNode } from "react";
import { Text } from "@mantine/core";

type Props = {
  label: ReactNode;
  current: ReactNode;
  separator: ReactNode;
  required: ReactNode;
  suffix?: ReactNode;
  insufficient: boolean;
  normalColor: string;
  insufficientColor: string;
  size: "xs" | "sm";
  testId?: string;
};

// 現在値と要求値を既存書式で並べ、不足時の色だけを共通判定する
// Keeps each current/required format while centralizing only the lack color decision
export default function LackHighlightText({ label, current, separator, required, suffix, insufficient, normalColor, insufficientColor, size, testId }: Props) {
  return (
    <Text size={size} c={insufficient ? insufficientColor : normalColor} data-testid={testId}>
      {label}{current}{separator}{required}{suffix}
    </Text>
  );
}
