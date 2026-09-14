// 開始を止める全画面ゲートの外殻。待機の購読と「待っていなければ何も描かない」をここ1本が持つ
// The shell of a full-screen start gate; this single place owns the waiting subscription and the render-nothing rule
import type { ReactElement, ReactNode } from "react";
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { Topics, useTopicSelector } from "@/bridge";
import styles from "./style.module.css";

// 待機の有無だけを見る全画面ゲートのtopic族。ここに載る3つ以外は外殻を共有しない
// The topic family of full-screen gates, read only for their waiting flag; nothing outside these three shares the shell
export type GateTopic =
  | typeof Topics.eventLanguageGate
  | typeof Topics.crashReportGate
  | typeof Topics.consentGate;

type Props = {
  topic: GateTopic;
  testId: string;
  title: ReactNode;
  children: ReactNode;
};

export default function FullScreenGate({ topic, testId, title, children }: Props): ReactElement | null {
  const waiting = useTopicSelector(topic, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--event-language-gate-face)"
        zIndex="var(--z-portal-event-language-gate)"
        data-testid={testId}
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white" ta="center" className={styles.title} data-testid={`${testId}-title`}>{title}</Title>
          {children}
        </Stack>
      </Overlay>
    </Portal>
  );
}
