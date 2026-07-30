import type { ReactNode } from "react";
import { Text } from "@mantine/core";
import styles from "./style.module.css";

type Props = {
  children: ReactNode;
  insufficient: boolean;
  size: "xs" | "sm";
  testId?: string;
};

// 現在値と要求値を既存書式で並べ、不足時の色だけを共通判定する
// Keeps each current/required format while centralizing only the lack color decision
export default function LackHighlightText({ children, insufficient, size, testId }: Props) {
  return (
    <Text className={styles.text} size={size} data-testid={testId} data-insufficient={insufficient}>
      {children}
    </Text>
  );
}
