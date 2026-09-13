// 待機中は全画面で操作を塞ぐ外殻。本体は待機中だけマウントする（EventLanguageGate と同型）
// The shell that blocks input full-screen while waiting; the body mounts only while waiting (same shape as EventLanguageGate)
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { Topics, useTopicSelector } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { CrashReportGateBody } from "./CrashReportGateBody";

export function CrashReportGate() {
  const { status, t } = useI18n();

  // ゲートは辞書配信より前に出るため、未確定の間は t() の空文字ではなく辞書非依存の文言を描く
  // The gate precedes dictionary delivery, so until it is ready the copy comes from dictionary-independent literals
  const title = status === "ready" ? t(L.ui.playtest.crashGate.title) : DictionaryIndependentText.crashGateTitle;
  const waiting = useTopicSelector(Topics.crashReportGate, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--playtest-gate-face)"
        zIndex="var(--z-portal-playtest-gate)"
        data-testid="crash-report-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{title}</Title>
          <CrashReportGateBody />
        </Stack>
      </Overlay>
    </Portal>
  );
}
