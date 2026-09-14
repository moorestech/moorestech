// 開始を止める全画面ゲートの外殻。待機の購読・ゲート同士の排他・「待っていなければ何も描かない」をここ1本が持つ
// The shell of a full-screen start gate; this single place owns the waiting subscription, the gates' exclusion and the render-nothing rule
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

// 先に答えさせる順。C#の起動順（言語→同意→前回異常終了）と同じで、同時に待っても手前の1枚しか描かない
// The order in which gates are answered, matching the C# boot order (language → consent → previous crash); only the frontmost renders
const GatePrecedence: readonly GateTopic[] = [Topics.eventLanguageGate, Topics.consentGate, Topics.crashReportGate];

type Props = {
  topic: GateTopic;
  testId: string;
  title: ReactNode;
  children: ReactNode;
};

export default function FullScreenGate({ topic, testId, title, children }: Props): ReactElement | null {
  const waiting: Record<GateTopic, boolean> = {
    [Topics.eventLanguageGate]: useTopicSelector(Topics.eventLanguageGate, (data) => data?.waiting ?? false),
    [Topics.consentGate]: useTopicSelector(Topics.consentGate, (data) => data?.waiting ?? false),
    [Topics.crashReportGate]: useTopicSelector(Topics.crashReportGate, (data) => data?.waiting ?? false),
  };

  // 2枚が同時に待つ経路は現行の起動順には無いが、無条件マウントなので排他はWeb側にも要る
  // No current boot path has two gates waiting at once, but they mount unconditionally, so the Web side needs the exclusion too
  if (GatePrecedence.find((candidate) => waiting[candidate]) !== topic) return null;

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
